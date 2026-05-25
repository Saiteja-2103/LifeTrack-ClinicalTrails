import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { UserInfo } from '../../../../core/models/auth.models';
import { DashboardService } from '../../../../core/services/dashboard.service';

@Component({
  selector: 'app-regulatory-dashboard',
  standalone: false,
  templateUrl: './regulatory-dashboard.component.html',
  styleUrls: ['./regulatory-dashboard.component.css']
})
export class RegulatoryDashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  user: UserInfo | null;

  today = new Date();

  totalDocs = 0; approvedDocs = 0; severeAEs = 0; kpiReports = 0;
  recentDocs: any[] = [];
  recentDeviations: any[] = [];
  loading = true;

  /** Percentage of approved documents out of total. */
  get complianceScore(): number {
    if (!this.totalDocs) return 0;
    return Math.round((this.approvedDocs / this.totalDocs) * 100);
  }

  /** Count of documents with "Under Review" or "Pending" status. */
  get pendingReviewDocs(): number {
    return this.recentDocs.filter(
      (d: any) => d.status === 'Under Review' || d.status === 'Pending'
    ).length;
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
      totalDocs:        this.ds.count('documents'),
      approvedDocs:     this.ds.count('documents', { status: 'Approved' }),
      severeAEs:        this.ds.count('adverse-events', { severity: 'Severe' }),
      kpiReports:       this.ds.count('kpi-reports'),
      recentDocs:       this.ds.list<any>('documents'),
      recentDeviations: this.ds.list<any>('deviations'),
    }).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.totalDocs        = d.totalDocs;
      this.approvedDocs     = d.approvedDocs;
      this.severeAEs        = d.severeAEs;
      this.kpiReports       = d.kpiReports;
      this.recentDocs       = d.recentDocs;
      this.recentDeviations = d.recentDeviations;
      this.loading          = false;
    });
  }

  get firstName() { return this.user?.name?.split(' ')[0] ?? this.user?.name; }

  viewDocuments()    { this.router.navigate(['/dashboard/documents']); }
  viewProtocols()    { this.router.navigate(['/dashboard/protocols']); }
  viewAdverseEvents(){ this.router.navigate(['/dashboard/adverse-events']); }
  viewAuditLogs()    { this.router.navigate(['/dashboard/audit-logs']); }

  docStatusClass(s: string): string {
    const m: Record<string, string> = {
      Approved: 'badge-green', 'Under Review': 'badge-amber',
      Pending: 'badge-amber', Draft: 'badge-slate', Rejected: 'badge-red'
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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
