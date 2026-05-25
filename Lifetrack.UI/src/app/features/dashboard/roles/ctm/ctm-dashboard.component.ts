import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { UserInfo } from '../../../../core/models/auth.models';
import { DashboardService } from '../../../../core/services/dashboard.service';

@Component({
  selector: 'app-ctm-dashboard',
  standalone: false,
  templateUrl: './ctm-dashboard.component.html',
  styleUrls: ['./ctm-dashboard.component.css']
})
export class CtmDashboardComponent implements OnInit, OnDestroy {
  user: UserInfo | null;
  private destroy$ = new Subject<void>();

  today = new Date();

  protocols = 0; activeEnrollments = 0; openAEs = 0; pendingDocs = 0;
  recentProtocols: any[] = [];
  loading = true;

  constructor(
    private auth: AuthService,
    private ds: DashboardService,
    private router: Router
  ) {
    this.user = this.auth.currentUser;
  }

  ngOnInit() {
    forkJoin({
      protocols:         this.ds.count('protocols'),
      activeEnrollments: this.ds.count('enrollments', { status: 'Active' }),
      openAEs:           this.ds.count('adverse-events', { status: 'Open' }),
      pendingDocs:       this.ds.count('documents', { status: 'Under Review' }),
      recentProtocols:   this.ds.list<any>('protocols'),
    }).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.protocols         = d.protocols;
      this.activeEnrollments = d.activeEnrollments;
      this.openAEs           = d.openAEs;
      this.pendingDocs       = d.pendingDocs;
      this.recentProtocols   = d.recentProtocols;
      this.loading           = false;
    });
  }

  get firstName() { return this.user?.name?.split(' ')[0] ?? this.user?.name; }

  manageProtocols()     { this.router.navigate(['/dashboard/protocols']); }
  manageSites()         { this.router.navigate(['/dashboard/sites']); }
  manageAssignments()   { this.router.navigate(['/dashboard/assignments']); }
  manageAdverseEvents() { this.router.navigate(['/dashboard/adverse-events']); }
  manageReports()       { this.router.navigate(['/dashboard/reports']); }

  statusClass(s: string): string {
    const m: Record<string, string> = {
      Active: 'badge-green', Completed: 'badge-blue',
      Paused: 'badge-amber', Draft: 'badge-slate', Terminated: 'badge-red'
    };
    return m[s] ?? 'badge-slate';
  }

  /** Returns a CSS width string for the protocol progress bar based on status. */
  protocolBarWidth(status: string): string {
    const m: Record<string, string> = {
      Active: '75%', Completed: '100%',
      Paused: '50%', Draft: '20%', Terminated: '30%'
    };
    return m[status] ?? '40%';
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
