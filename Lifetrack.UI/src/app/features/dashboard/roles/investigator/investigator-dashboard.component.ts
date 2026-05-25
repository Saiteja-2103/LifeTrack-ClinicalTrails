import { Component, OnDestroy, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { forkJoin, Subject } from 'rxjs';
import { catchError, of, takeUntil } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { UserInfo } from '../../../../core/models/auth.models';
import { DashboardService } from '../../../../core/services/dashboard.service';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-investigator-dashboard',
  standalone: false,
  templateUrl: './investigator-dashboard.component.html',
  styleUrls: ['./investigator-dashboard.component.css']
})
export class InvestigatorDashboardComponent implements OnInit, OnDestroy {
  user: UserInfo | null;
  /** Signal that completes all subscriptions on component destroy. */
  private destroy$ = new Subject<void>();

  today = new Date();

  // ── Stats ──────────────────────────────────────────────────────────────────
  sites = 0; patients = 0; adverseEvents = 0; visits = 0;
  loading = true;

  // ── My Assignments ─────────────────────────────────────────────────────────
  myAssignments: any[] = [];
  protocolMap: Record<number, string> = {};
  siteMap: Record<number, string>     = {};
  assignmentsLoading = true;

  // ── Recent Adverse Events ──────────────────────────────────────────────────
  recentAEs:   any[] = [];
  aesLoading = true;

  constructor(
    private auth: AuthService,
    private ds: DashboardService,
    private http: HttpClient,
    private router: Router
  ) {
    this.user = this.auth.currentUser;
  }

  ngOnInit() {
    // General stats + recent AEs
    forkJoin({
      sites:         this.ds.count('sites'),
      patients:      this.ds.count('patients'),
      adverseEvents: this.ds.count('adverse-events'),
      visits:        this.ds.count('visits'),
      recentAEs:     this.ds.list<any>('adverse-events'),
    }).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.sites = d.sites; this.patients = d.patients;
      this.adverseEvents = d.adverseEvents; this.visits = d.visits;
      this.recentAEs = d.recentAEs;
      this.loading   = false;
      this.aesLoading = false;
    });

    // My protocol + site assignments
    const uid = this.user?.userID;
    if (uid) {
      forkJoin({
        assignments: this.http.get<any>(`${environment.apiUrl}/site-protocols?investigatorId=${uid}&pageSize=50`)
          .pipe(catchError(() => of({ items: [] }))),
        protocols:   this.http.get<any>(`${environment.apiUrl}/protocols?pageSize=200`)
          .pipe(catchError(() => of({ items: [] }))),
        sites:       this.http.get<any>(`${environment.apiUrl}/sites?pageSize=200`)
          .pipe(catchError(() => of({ items: [] }))),
      }).pipe(takeUntil(this.destroy$)).subscribe(({ assignments, protocols, sites }) => {
        this.myAssignments = assignments.items ?? [];
        const pm: Record<number, string> = {};
        (protocols.items ?? []).forEach((p: any) => pm[p.protocolID] = p.title);
        this.protocolMap = pm;
        const sm: Record<number, string> = {};
        (sites.items ?? []).forEach((s: any) => sm[s.siteID] = s.name);
        this.siteMap = sm;
        this.assignmentsLoading = false;
      });
    } else {
      this.assignmentsLoading = false;
    }
  }

  get firstName() { return this.user?.name?.split(' ')[0] ?? this.user?.name; }

  manageEnrollments()   { this.router.navigate(['/dashboard/enrollments']); }
  manageVisits()        { this.router.navigate(['/dashboard/visits']); }
  manageAdverseEvents() { this.router.navigate(['/dashboard/adverse-events']); }
  manageDeviations()    { this.router.navigate(['/dashboard/deviations']); }

  protocolName(id: number)  { return this.protocolMap[id] ?? `Protocol #${id}`; }
  siteName(id: number)      { return this.siteMap[id]     ?? `Site #${id}`; }

  assignmentStatusClass(s: string): string {
    const m: Record<string, string> = {
      Active: 'badge-green', Pending: 'badge-amber',
      Suspended: 'badge-red', Completed: 'badge-blue'
    };
    return m[s] ?? 'badge-slate';
  }

  aeSeverityClass(s: string): string {
    const m: Record<string, string> = {
      'Life-Threatening': 'badge-red', Severe: 'badge-red',
      Moderate: 'badge-amber', Mild: 'badge-green'
    };
    return m[s] ?? 'badge-slate';
  }

  aeStatusClass(s: string): string {
    const m: Record<string, string> = {
      Open: 'badge-red', 'Under Review': 'badge-amber',
      Resolved: 'badge-green', Closed: 'badge-slate'
    };
    return m[s] ?? 'badge-slate';
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
