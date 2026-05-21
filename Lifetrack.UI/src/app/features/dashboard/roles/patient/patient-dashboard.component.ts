import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { UserInfo } from '../../../../core/models/auth.models';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-patient-dashboard',
  standalone: false,
  templateUrl: './patient-dashboard.component.html',
  styleUrls: ['./patient-dashboard.component.css']
})
export class PatientDashboardComponent implements OnInit {
  user: UserInfo | null;

  today = new Date();

  // ── Patient record (resolved from UserID) ─────────────────────────────────
  patientRecord: any = null;
  noPatientRecord   = false;

  // ── Data panels ───────────────────────────────────────────────────────────
  enrollments:    any[] = [];
  upcomingVisits: any[] = [];
  adverseEvents:  any[] = [];
  notifications:  any[] = [];

  // ── Lookup maps ───────────────────────────────────────────────────────────
  protocolMap:     Record<number, string> = {};
  siteProtocolMap: Record<number, any>    = {};
  siteMap:         Record<number, string> = {};

  // ── Stats ─────────────────────────────────────────────────────────────────
  statsEnrollments       = 0;
  statsActiveEnrollments = 0;
  statsUpcomingVisits    = 0;
  statsNotifications     = 0;

  loading        = true;
  detailsLoading = true;

  constructor(private auth: AuthService, private http: HttpClient) {
    this.user = this.auth.currentUser;
  }

  ngOnInit(): void {
    const uid = this.user?.userID;

    // Phase 1: find this user's PatientRecord + load notifications
    forkJoin({
      patients:      this.http.get<any>(`${environment.apiUrl}/patients?pageSize=200`)
                       .pipe(catchError(() => of({ items: [] }))),
      notifications: this.http.get<any>(`${environment.apiUrl}/notifications/my?pageSize=6`)
                       .pipe(catchError(() => of({ items: [] }))),
    }).subscribe(({ patients, notifications }) => {

      this.notifications    = notifications.items ?? [];
      this.statsNotifications = this.notifications.length;

      const match = (patients.items ?? []).find((p: any) => p.userID === uid);
      if (!match) {
        this.noPatientRecord = true;
        this.loading         = false;
        this.detailsLoading  = false;
        return;
      }

      this.patientRecord = match;
      this.loading       = false;   // profile / stats spinner off
      this.loadClinicalData(match.patientID);
    });
  }

  private loadClinicalData(patientID: number): void {
    forkJoin({
      enrollments:   this.http.get<any>(`${environment.apiUrl}/enrollments?patientId=${patientID}&pageSize=50`)
                       .pipe(catchError(() => of({ items: [] }))),
      adverseEvents: this.http.get<any>(`${environment.apiUrl}/adverse-events?patientId=${patientID}&pageSize=50`)
                       .pipe(catchError(() => of({ items: [] }))),
      protocols:     this.http.get<any>(`${environment.apiUrl}/protocols?pageSize=200`)
                       .pipe(catchError(() => of({ items: [] }))),
      siteProtocols: this.http.get<any>(`${environment.apiUrl}/site-protocols?pageSize=200`)
                       .pipe(catchError(() => of({ items: [] }))),
      sites:         this.http.get<any>(`${environment.apiUrl}/sites?pageSize=200`)
                       .pipe(catchError(() => of({ items: [] }))),
      allVisits:     this.http.get<any>(`${environment.apiUrl}/visits?pageSize=200`)
                       .pipe(catchError(() => of({ items: [] }))),
    }).subscribe(({ enrollments, adverseEvents, protocols, siteProtocols, sites, allVisits }) => {

      // Build lookup maps
      (protocols.items ?? []).forEach((p: any) => this.protocolMap[p.protocolID] = p.title);
      (sites.items     ?? []).forEach((s: any) => this.siteMap[s.siteID]         = s.name);
      (siteProtocols.items ?? []).forEach((sp: any) => this.siteProtocolMap[sp.siteProtocolID] = sp);

      this.enrollments  = enrollments.items  ?? [];
      this.adverseEvents= adverseEvents.items ?? [];

      // Filter visits to this patient's enrollments, only Scheduled, sorted by date
      const myEnrollmentIDs = new Set<number>(this.enrollments.map((e: any) => e.enrollmentID));
      this.upcomingVisits = (allVisits.items ?? [])
        .filter((v: any) => myEnrollmentIDs.has(v.enrollmentID) && v.status === 'Scheduled')
        .sort((a: any, b: any) => new Date(a.visitDate).getTime() - new Date(b.visitDate).getTime())
        .slice(0, 6);

      // Stats
      this.statsEnrollments       = this.enrollments.length;
      this.statsActiveEnrollments = this.enrollments.filter((e: any) => e.status === 'Active').length;
      this.statsUpcomingVisits    = this.upcomingVisits.length;

      this.detailsLoading = false;
    });
  }

  // ── Getters ───────────────────────────────────────────────────────────────
  get firstName() { return this.user?.name?.split(' ')[0] ?? this.user?.name; }

  /** Two-letter initials for the avatar circle. */
  get initials(): string {
    const name = this.user?.name ?? '';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 0 || !parts[0]) return '?';
    const a = parts[0].charAt(0);
    const b = parts.length > 1 ? parts[parts.length - 1].charAt(0) : '';
    return (a + b).toUpperCase();
  }

  /** Active enrollment's protocol title, or a fallback. */
  get activeStudyName(): string {
    const active = this.enrollments.find((e: any) => e.status === 'Active') ?? this.enrollments[0];
    if (!active) return 'Clinical trial participant';
    return this.protocolName(active.siteProtocolID);
  }

  /** The soonest upcoming scheduled visit. */
  get nextVisit(): any {
    return this.upcomingVisits[0] ?? null;
  }

  /** Two-letter avatar for a message sender, derived from category text. */
  messageAvatar(n: any): string {
    const src = n?.category ?? n?.from ?? 'SY';
    const parts = String(src).trim().split(/\s+/);
    const a = parts[0]?.charAt(0) ?? '?';
    const b = parts[1]?.charAt(0) ?? '';
    return (a + b).toUpperCase();
  }

  get formattedDOB(): string {
    if (!this.patientRecord?.dob) return '—';
    return new Date(this.patientRecord.dob).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' });
  }

  // ── Lookup helpers ────────────────────────────────────────────────────────
  protocolName(siteProtocolID: number): string {
    const sp = this.siteProtocolMap[siteProtocolID];
    if (!sp) return `SP #${siteProtocolID}`;
    return this.protocolMap[sp.protocolID] ?? `Protocol #${sp.protocolID}`;
  }

  siteName(siteProtocolID: number): string {
    const sp = this.siteProtocolMap[siteProtocolID];
    if (!sp) return '—';
    return this.siteMap[sp.siteID] ?? `Site #${sp.siteID}`;
  }

  visitProtocolName(enrollmentID: number): string {
    const e = this.enrollments.find((e: any) => e.enrollmentID === enrollmentID);
    return e ? this.protocolName(e.siteProtocolID) : `Enrollment #${enrollmentID}`;
  }

  // ── Badge helpers ─────────────────────────────────────────────────────────
  enrollmentStatusClass(s: string): string {
    const m: Record<string, string> = {
      Active: 'badge-green', Completed: 'badge-blue',
      Screening: 'badge-cyan', Withdrawn: 'badge-red'
    };
    return m[s] ?? 'badge-slate';
  }

  visitStatusClass(s: string): string {
    const m: Record<string, string> = {
      Scheduled: 'badge-blue', Completed: 'badge-green',
      Missed: 'badge-amber',   Cancelled: 'badge-red'
    };
    return m[s] ?? 'badge-slate';
  }

  aeSeverityClass(s: string): string {
    const m: Record<string, string> = {
      Mild: 'badge-amber', Moderate: 'badge-amber',
      Severe: 'badge-red', 'Life-Threatening': 'badge-red'
    };
    return m[s] ?? 'badge-slate';
  }

  aeStatusClass(s: string): string {
    const m: Record<string, string> = {
      Reported: 'badge-amber', 'Under Review': 'badge-blue',
      Resolved: 'badge-green', Closed: 'badge-slate'
    };
    return m[s] ?? 'badge-slate';
  }
}
