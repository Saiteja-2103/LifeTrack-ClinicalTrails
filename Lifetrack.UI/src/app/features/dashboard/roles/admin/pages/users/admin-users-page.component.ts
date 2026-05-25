import { Component, OnDestroy, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../../../../core/services/navigation.service';
import { AuthService } from '../../../../../../core/services/auth.service';
import { environment } from '../../../../../../../environments/environment';

@Component({
  selector: 'app-admin-users-page',
  standalone: false,
  templateUrl: './admin-users-page.component.html',
  styleUrls: ['./admin-users-page.component.css']
})
export class AdminUsersPageComponent implements OnInit, OnDestroy {

  // ── List state ─────────────────────────────────────────────────────────────
  userList: any[]  = [];
  listLoading      = false;
  listPage         = 1;
  listPageSize     = 10;
  listTotalCount   = 0;
  listTotalPages   = 1;
  searchTerm       = '';
  private searchTimer: any;

  // ── Create User modal ──────────────────────────────────────────────────────
  showCreateModal  = false;
  createForm!: FormGroup;
  createSubmitting = false;
  createError      = '';
  createSuccess    = false;

  // ── Edit User modal ────────────────────────────────────────────────────────
  showEditModal  = false;
  editingUser: any = null;
  editForm!: FormGroup;
  editSubmitting = false;
  editError      = '';
  editSuccess    = false;

  // ── Delete confirmation ────────────────────────────────────────────────────
  showDeleteConfirm = false;
  deletingUser: any = null;
  deleteLoading     = false;
  deleteError       = '';

  /** Admin can create any role except Patient (ID 4) — patients self-register. */
  readonly roles = [
    { id: 1, label: 'Admin' },
    { id: 2, label: 'Clinical Trial Manager' },
    { id: 3, label: 'Investigator' },
    { id: 5, label: 'Regulatory Officer' },
    { id: 6, label: 'Data Manager' },
  ];

  private currentUserId: number | null = null;

  constructor(
    private http: HttpClient,
    private fb: FormBuilder,
    private router: Router,
    private auth: AuthService,
    private nav: NavigationService
  ) {
    this.currentUserId = this.auth.currentUser?.userID ?? null;
  }

  ngOnInit() {
    this.createForm = this.fb.group({
      name:     ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
      email:    ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
      phone:    ['', Validators.maxLength(32)],
      roleID:   [null, Validators.required],
    });

    this.editForm = this.fb.group({
      name:   ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
      phone:  ['', Validators.maxLength(32)],
      roleID: [null, Validators.required],
    });

    this.loadUsers(1);
  }

  // ── Navigation ─────────────────────────────────────────────────────────────
  goBack() { this.nav.back('/dashboard/admin'); }

  // ── Search ─────────────────────────────────────────────────────────────────
  onSearch(value: string) {
    this.searchTerm = value;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.loadUsers(1), 400);
  }

  clearSearch() {
    this.searchTerm = '';
    this.loadUsers(1);
  }

  // ── User List ──────────────────────────────────────────────────────────────
  loadUsers(page: number) {
    this.listLoading = true;
    this.listPage    = page;
    const qs = new URLSearchParams({ page: String(page), pageSize: String(this.listPageSize) });
    if (this.searchTerm.trim()) qs.set('search', this.searchTerm.trim());

    this.http.get<any>(`${environment.apiUrl}/users?${qs}`).subscribe({
      next: r => {
        this.userList       = r.items ?? [];
        this.listTotalCount = r.totalCount ?? 0;
        this.listTotalPages = r.totalPages ?? 1;
        this.listLoading    = false;
      },
      error: () => { this.listLoading = false; }
    });
  }

  get pageNumbers(): number[] {
    return Array.from({ length: this.listTotalPages }, (_, i) => i + 1);
  }

  // ── Create User ────────────────────────────────────────────────────────────
  openCreateModal() {
    this.createForm.reset();
    this.createError   = '';
    this.createSuccess = false;
    this.showCreateModal = true;
  }

  closeCreateModal() {
    if (this.createSubmitting) return;
    this.showCreateModal = false;
  }

  onSubBackdrop(e: MouseEvent, closer: () => void) {
    if ((e.target as HTMLElement).classList.contains('sub-backdrop')) closer();
  }

  submitCreate() {
    if (this.createForm.invalid) { this.createForm.markAllAsTouched(); return; }
    this.createSubmitting = true;
    this.createError      = '';
    const v = this.createForm.value;
    const body: any = {
      name:     v.name.trim(),
      email:    v.email.trim(),
      password: v.password,
      roleID:   Number(v.roleID),
    };
    if (v.phone?.trim()) body.phone = v.phone.trim();

    this.http.post(`${environment.apiUrl}/auth/register`, body).subscribe({
      next: () => {
        this.createSubmitting = false;
        this.createSuccess    = true;
        this.listTotalCount++;
        setTimeout(() => {
          this.showCreateModal = false;
          this.createSuccess   = false;
          this.loadUsers(this.listPage);
        }, 1500);
      },
      error: err => {
        this.createSubmitting = false;
        this.createError = err?.error?.error ?? err?.error?.message ?? 'Failed to create user.';
      }
    });
  }

  // ── Edit User ──────────────────────────────────────────────────────────────
  openEditModal(u: any) {
    this.editingUser = u;
    this.editForm.patchValue({ name: u.name, phone: u.phone ?? '', roleID: u.roleID });
    this.editError   = '';
    this.editSuccess = false;
    this.showEditModal = true;
  }

  closeEditModal() {
    if (this.editSubmitting) return;
    this.showEditModal = false;
    this.editingUser   = null;
  }

  submitEdit() {
    if (this.editForm.invalid) { this.editForm.markAllAsTouched(); return; }
    this.editSubmitting = true;
    this.editError      = '';
    const v = this.editForm.value;
    const body: any = { name: v.name.trim(), roleID: Number(v.roleID) };
    if (v.phone?.trim()) body.phone = v.phone.trim();

    this.http.put<any>(`${environment.apiUrl}/users/${this.editingUser.userID}`, body).subscribe({
      next: updated => {
        this.editSubmitting = false;
        this.editSuccess    = true;
        const idx = this.userList.findIndex(u => u.userID === this.editingUser.userID);
        if (idx > -1) this.userList[idx] = updated;
        setTimeout(() => {
          this.showEditModal = false;
          this.editSuccess   = false;
          this.editingUser   = null;
        }, 1500);
      },
      error: err => {
        this.editSubmitting = false;
        this.editError = err?.error?.error ?? err?.error?.message ?? 'Failed to update user.';
      }
    });
  }

  // ── Reactivate User ────────────────────────────────────────────────────────
  reactivateLoading: Record<number, boolean> = {};

  reactivate(u: any) {
    if (this.reactivateLoading[u.userID]) return;
    this.reactivateLoading[u.userID] = true;
    this.http.put<any>(`${environment.apiUrl}/users/${u.userID}/reactivate`, {}).subscribe({
      next: updated => {
        this.reactivateLoading[u.userID] = false;
        const idx = this.userList.findIndex(x => x.userID === u.userID);
        if (idx > -1) this.userList[idx] = updated;
      },
      error: () => { this.reactivateLoading[u.userID] = false; }
    });
  }

  // ── Delete User (sets IsActive = false — blocks login) ────────────────────
  openDeleteConfirm(u: any) {
    this.deletingUser = u;
    this.deleteError  = '';
    this.showDeleteConfirm = true;
  }

  closeDeleteConfirm() {
    if (this.deleteLoading) return;
    this.showDeleteConfirm = false;
    this.deletingUser      = null;
  }

  confirmDelete() {
    if (!this.deletingUser) return;
    this.deleteLoading = true;
    this.deleteError   = '';

    this.http.delete(`${environment.apiUrl}/users/${this.deletingUser.userID}`).subscribe({
      next: () => {
        this.deleteLoading     = false;
        this.showDeleteConfirm = false;
        this.deletingUser      = null;
        this.listTotalCount    = Math.max(0, this.listTotalCount - 1);
        const newPages   = Math.ceil(this.listTotalCount / this.listPageSize) || 1;
        const targetPage = this.listPage > newPages ? newPages : this.listPage;
        this.loadUsers(targetPage);
      },
      error: err => {
        this.deleteLoading = false;
        this.deleteError = err?.error?.error ?? err?.error?.message ?? 'Failed to delete user.';
      }
    });
  }

  // ── Helpers ────────────────────────────────────────────────────────────────
  isSelf(u: any): boolean       { return u?.userID === this.currentUserId; }
  isSuperAdmin(u: any): boolean { return u?.userID === 1; }
  isProtected(u: any): boolean  { return this.isSelf(u) || this.isSuperAdmin(u); }

  roleDisplay(role: string): string {
    const m: Record<string, string> = {
      ClinicalTrialManager: 'CTM',
      RegulatoryOfficer:    'Reg. Officer',
      DataManager:          'Data Mgr',
    };
    return m[role] ?? role;
  }

  roleClass(role: string): string {
    const m: Record<string, string> = {
      Admin:                'badge-purple',
      ClinicalTrialManager: 'badge-blue',
      Investigator:         'badge-cyan',
      Patient:              'badge-green',
      RegulatoryOfficer:    'badge-amber',
      DataManager:          'badge-red',
    };
    return m[role] ?? 'badge-slate';
  }

  cf(n: string) { return this.createForm.get(n)!; }
  ef(n: string) { return this.editForm.get(n)!; }

  fieldErr(ctrl: any): string {
    if (!ctrl.touched || ctrl.valid) return '';
    if (ctrl.errors?.['required'])  return 'Required.';
    if (ctrl.errors?.['email'])     return 'Invalid email address.';
    if (ctrl.errors?.['minlength']) return `Min ${ctrl.errors['minlength'].requiredLength} characters.`;
    if (ctrl.errors?.['maxlength']) return `Max ${ctrl.errors['maxlength'].requiredLength} characters.`;
    return 'Invalid value.';
  }

  ngOnDestroy(): void {
    // Cancel pending search debounce + success-flash timers so they can't fire
    // on a destroyed component.
    clearTimeout(this.searchTimer);
  }
}
