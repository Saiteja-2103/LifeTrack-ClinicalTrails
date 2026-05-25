import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { UserInfo } from '../../../../core/models/auth.models';
import { DashboardService } from '../../../../core/services/dashboard.service';

@Component({
  selector: 'app-admin-dashboard',
  standalone: false,
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  user: UserInfo | null;

  today = new Date();

  // ── Dashboard stats ───────────────────────────────────────────────────────
  users = 0; protocols = 0; patients = 0; sites = 0;
  recentAEs: any[] = [];
  recentUsers: any[] = [];
  recentProtocols: any[] = [];
  loading = true;

  // ── User filter ──────────────────────────────────────────────────────────
  userSearch = '';

  // ── AE Detail Modal ───────────────────────────────────────────────────────
  selectedAE: any = null;
  showAEModal = false;

  constructor(
    private auth: AuthService,
    private ds: DashboardService,
    private router: Router
  ) {
    this.user = this.auth.currentUser;
  }

  ngOnInit() {
    forkJoin({
      users:           this.ds.count('users'),
      protocols:       this.ds.count('protocols'),
      patients:        this.ds.count('patients'),
      sites:           this.ds.count('sites'),
      recentAEs:       this.ds.list<any>('adverse-events'),
      recentUsers:     this.ds.list<any>('users'),
      recentProtocols: this.ds.list<any>('protocols'),
    }).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.users           = d.users;
      this.protocols       = d.protocols;
      this.patients        = d.patients;
      this.sites           = d.sites;
      this.recentAEs       = d.recentAEs;
      this.recentUsers     = d.recentUsers;
      this.recentProtocols = d.recentProtocols;
      this.loading         = false;
    });
  }

  get firstName() { return this.user?.name?.split(' ')[0] ?? this.user?.name; }

  manageUsers()        { this.router.navigate(['/dashboard/admin/users']); }
  manageProtocols()    { this.router.navigate(['/dashboard/admin/protocols']); }
  manageSites()        { this.router.navigate(['/dashboard/admin/sites']); }
  manageAssignments()  { this.router.navigate(['/dashboard/admin/assignments']); }
  manageDocuments()    { this.router.navigate(['/dashboard/admin/documents']); }
  manageReports()      { this.router.navigate(['/dashboard/admin/reports']); }
  manageAuditLogs()    { this.router.navigate(['/dashboard/audit-logs']); }

  // ── AE Detail Modal helpers ───────────────────────────────────────────────
  openAEDetail(ae: any): void {
    this.selectedAE  = ae;
    this.showAEModal = true;
  }
  closeAEModal(): void {
    this.showAEModal = false;
    this.selectedAE  = null;
  }

  // ── Badge / display helpers ───────────────────────────────────────────────
  severityClass(s: string): string {
    const m: Record<string, string> = {
      Critical: 'badge-red', Severe: 'badge-red',
      Moderate: 'badge-amber', Mild: 'badge-green'
    };
    return m[s] ?? 'badge-slate';
  }

  statusClass(s: string): string {
    const m: Record<string, string> = {
      Open: 'badge-red', 'Under Review': 'badge-amber', Resolved: 'badge-green'
    };
    return m[s] ?? 'badge-slate';
  }

  /** Filtered list of users for the table by name/email search. */
  get filteredUsers(): any[] {
    const term = this.userSearch.trim().toLowerCase();
    if (!term) return this.recentUsers.slice(0, 6);
    return this.recentUsers
      .filter(u =>
        (u.name ?? '').toLowerCase().includes(term) ||
        (u.email ?? '').toLowerCase().includes(term) ||
        (u.role ?? '').toLowerCase().includes(term))
      .slice(0, 6);
  }

  /** Active protocols count for KPI sub-text. */
  get activeProtocols(): number {
    return this.recentProtocols.filter(p => p.status === 'Active').length;
  }

  /** Count of active users in the fetched list. */
  get activeUsersInList(): number {
    return this.recentUsers.filter(u => u.isActive).length;
  }

  /** Draft protocols count for KPI sub-text. */
  get draftProtocols(): number {
    return this.recentProtocols.filter(p => p.status === 'Draft').length;
  }

  /** Coloured pill class for a user's role. */
  roleClass(role: string): string {
    const m: Record<string, string> = {
      Admin:        'badge-purple',
      Investigator: 'badge-green',
      CTM:          'badge-blue',
      'Trial Manager': 'badge-blue',
      Regulatory:   'badge-purple',
      'Regulatory Officer': 'badge-purple',
      'Data Manager': 'badge-amber',
      Patient:      'badge-cyan'
    };
    return m[role] ?? 'badge-slate';
  }

  /** Coloured pill class for a user's status. */
  userStatusClass(s: string): string {
    const m: Record<string, string> = {
      Active: 'badge-green', Pending: 'badge-amber',
      Suspended: 'badge-red', Inactive: 'badge-slate'
    };
    return m[s] ?? 'badge-slate';
  }

  /** Coloured pill class for protocol status. */
  protocolStatusClass(s: string): string {
    const m: Record<string, string> = {
      Active: 'badge-green', Completed: 'badge-blue',
      Paused: 'badge-amber', Draft: 'badge-slate', Terminated: 'badge-red'
    };
    return m[s] ?? 'badge-slate';
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
