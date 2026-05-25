import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { UserInfo } from '../../../../core/models/auth.models';
import { DashboardService } from '../../../../core/services/dashboard.service';

@Component({
  selector: 'app-data-manager-dashboard',
  standalone: false,
  templateUrl: './data-manager-dashboard.component.html',
  styleUrls: ['./data-manager-dashboard.component.css']
})
export class DataManagerDashboardComponent implements OnInit, OnDestroy {
  user: UserInfo | null;
  private destroy$ = new Subject<void>();

  today = new Date();

  // ── Stats ─────────────────────────────────────────────────────────────────
  openAEs    = 0;
  deviations = 0;
  draftDocs  = 0;
  kpiReports = 0;
  protocolsCount = 0;

  // ── Data panels ───────────────────────────────────────────────────────────
  openAEList:   any[] = [];
  recentDeviations: any[] = [];
  loading = true;

  // ── Query filter ─────────────────────────────────────────────────────────
  queryFilter = 'all';

  /** Live: AE counts grouped by protocol (built from `openAEList` after load). */
  aesByProtocol: Array<{ name: string; count: number; band: 'good' | 'warn' | 'bad' }> = [];

  /** Live: validation issues derived from real counters. */
  get validationIssues() {
    return [
      { name: 'Open adverse events', count: this.openAEs,    band: this.openAEs    > 5 ? 'danger' : 'warn',  detail: 'Awaiting investigator review' },
      { name: 'Protocol deviations', count: this.deviations, band: this.deviations > 5 ? 'danger' : 'warn',  detail: 'Reported across all sites' },
      { name: 'Draft documents',     count: this.draftDocs,  band: this.draftDocs  > 0 ? 'warn'   : 'slate', detail: 'Not yet published' },
      { name: 'Outstanding reports', count: this.kpiReports, band: 'slate', detail: 'KPI snapshots ready to view' }
    ];
  }

  /** Aggregate data quality (rough proxy from open issues). */
  get dataQualityScore(): number {
    const open = this.openAEs + this.deviations;
    if (open === 0) return 100;
    // Simple inverse score: more open items → lower quality. Caps to [70, 99.9].
    const raw = 100 - Math.min(open * 0.6, 30);
    return Math.round(raw * 10) / 10;
  }

  /** Count of overdue queries (proxy: deviations marked Open). */
  get overdueQueries(): number {
    return this.recentDeviations.filter(d => d.status === 'Open').length;
  }

  constructor(
    private auth: AuthService,
    private ds: DashboardService,
    private router: Router
  ) {
    this.user = this.auth.currentUser;
  }

  ngOnInit() {
    forkJoin({
      openAEs:    this.ds.count('adverse-events', { status: 'Open' }),
      deviations: this.ds.count('deviations'),
      draftDocs:  this.ds.count('documents', { status: 'Draft' }),
      kpiReports: this.ds.count('kpi-reports'),
      protocols:  this.ds.count('protocols'),
      openAEList: this.ds.list<any>('adverse-events', { status: 'Open' }),
      recentDeviations: this.ds.list<any>('deviations'),
    }).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.openAEs    = d.openAEs;
      this.deviations = d.deviations;
      this.draftDocs  = d.draftDocs;
      this.kpiReports = d.kpiReports;
      this.protocolsCount = d.protocols;
      this.openAEList = d.openAEList;
      this.recentDeviations = d.recentDeviations;

      // Build live AE-by-protocol breakdown from real data
      const groups: Record<number, number> = {};
      d.openAEList.forEach((ae: any) => {
        const pid = ae.protocolID ?? 0;
        groups[pid] = (groups[pid] ?? 0) + 1;
      });
      const max = Math.max(1, ...Object.values(groups));
      this.aesByProtocol = Object.entries(groups)
        .map(([pid, count]) => ({
          name:  `Protocol #${pid}`,
          count,
          band:  (count / max > 0.66 ? 'bad' : count / max > 0.33 ? 'warn' : 'good') as 'good' | 'warn' | 'bad'
        }))
        .sort((a, b) => b.count - a.count)
        .slice(0, 5);

      this.loading    = false;
    });
  }

  get firstName() { return this.user?.name?.split(' ')[0] ?? this.user?.name; }

  // ── Quick Actions ─────────────────────────────────────────────────────────
  viewAdverseEvents() { this.router.navigate(['/dashboard/adverse-events']); }
  viewDeviations()    { this.router.navigate(['/dashboard/deviations']); }
  viewEnrollments()   { this.router.navigate(['/dashboard/enrollments']); }
  viewReports()       { this.router.navigate(['/dashboard/reports']); }

  // ── Badge helpers ─────────────────────────────────────────────────────────
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

  devSeverityClass(s: string): string {
    const m: Record<string, string> = {
      Critical: 'badge-red', Major: 'badge-red',
      Minor: 'badge-amber', Administrative: 'badge-blue'
    };
    return m[s] ?? 'badge-slate';
  }

  devStatusClass(s: string): string {
    const m: Record<string, string> = {
      Open: 'badge-red', 'Under Review': 'badge-amber', Resolved: 'badge-green'
    };
    return m[s] ?? 'badge-slate';
  }

  /** Convert AE/deviation status to mockup "Awaiting / Overdue / Replied". */
  queryStatusClass(s: string): string {
    const m: Record<string, string> = {
      Open: 'badge-red', 'Under Review': 'badge-amber',
      Awaiting: 'badge-amber', Overdue: 'badge-red',
      Replied: 'badge-green', Resolved: 'badge-green'
    };
    return m[s] ?? 'badge-slate';
  }

  /** Combined list of "open queries to investigators" — AEs + deviations. */
  get openQueries(): any[] {
    const aes = this.openAEList.map(ae => ({
      ref:       `Patient #${ae.patientID}`,
      kind:      'AE',
      query:     ae.description ?? 'Clarify adverse event',
      sentTo:    'Investigator',
      date:      ae.reportedDate,
      status:    ae.status === 'Open' ? 'Awaiting' : 'Replied'
    }));
    const devs = this.recentDeviations.map(d => ({
      ref:       `Site protocol #${d.siteProtocolID}`,
      kind:      'Deviation',
      query:     d.description ?? 'Protocol deviation',
      sentTo:    'Site investigator',
      date:      null,           // Deviation DTO has no reportedDate
      status:    d.status === 'Open' ? 'Overdue' : 'Awaiting'
    }));
    const all = [...aes, ...devs];
    if (this.queryFilter === 'all') return all.slice(0, 6);
    if (this.queryFilter === 'overdue')  return all.filter(q => q.status === 'Overdue').slice(0, 6);
    if (this.queryFilter === 'awaiting') return all.filter(q => q.status === 'Awaiting').slice(0, 6);
    return all.slice(0, 6);
  }

  /** Days-ago for any date-based column (no longer used in the queries table). */
  daysAgo(date: any): string {
    if (!date) return '—';
    const d = new Date(date);
    const ms = Date.now() - d.getTime();
    const days = Math.floor(ms / 86400000);
    if (days <= 0) return 'today';
    if (days === 1) return '1 day';
    return `${days} days`;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
