import { Component, OnDestroy, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../../../../core/services/navigation.service';
import { environment } from '../../../../../../../environments/environment';

@Component({
  selector: 'app-admin-sites-page',
  standalone: false,
  templateUrl: './admin-sites-page.component.html',
  styleUrls: ['./admin-sites-page.component.css']
})
export class AdminSitesPageComponent implements OnInit, OnDestroy {

  // ── List state ─────────────────────────────────────────────────────────────
  siteList: any[]  = [];
  listLoading      = false;
  listPage         = 1;
  listPageSize     = 10;
  listTotalCount   = 0;
  listTotalPages   = 1;
  searchTerm       = '';
  filterStatus     = '';
  private searchTimer: any;

  readonly statuses = ['Pending', 'Active', 'Suspended', 'Closed'];

  // ── Create modal ───────────────────────────────────────────────────────────
  showCreateModal  = false;
  createForm!: FormGroup;
  createSubmitting = false;
  createError      = '';
  createSuccess    = false;

  // ── Edit modal ─────────────────────────────────────────────────────────────
  showEditModal  = false;
  editingItem: any = null;
  editForm!: FormGroup;
  editSubmitting = false;
  editError      = '';
  editSuccess    = false;

  // ── Delete modal ───────────────────────────────────────────────────────────
  showDeleteConfirm = false;
  deletingItem: any = null;
  deleteLoading     = false;
  deleteError       = '';

  constructor(
    private http: HttpClient,
    private fb: FormBuilder,
    private router: Router,
    private nav: NavigationService
  ) {}

  ngOnInit() {
    this.createForm = this.fb.group({
      name:     ['', [Validators.required, Validators.maxLength(300)]],
      location: ['', [Validators.required, Validators.maxLength(500)]],
      status:   ['Pending', Validators.required]
    });

    this.editForm = this.fb.group({
      name:     ['', [Validators.required, Validators.maxLength(300)]],
      location: ['', [Validators.required, Validators.maxLength(500)]],
      status:   ['', Validators.required]
    });

    this.loadSites(1);
  }

  goBack() { this.nav.back('/dashboard/admin'); }

  // ── Search / Filter ────────────────────────────────────────────────────────
  onSearch(value: string) {
    this.searchTerm = value;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.loadSites(1), 400);
  }

  onFilterChange() { this.loadSites(1); }

  clearFilters() {
    this.searchTerm = '';
    this.filterStatus = '';
    this.loadSites(1);
  }

  // ── List ───────────────────────────────────────────────────────────────────
  loadSites(page: number) {
    this.listLoading = true;
    this.listPage    = page;
    const qs = new URLSearchParams({ page: String(page), pageSize: String(this.listPageSize) });
    if (this.searchTerm.trim()) qs.set('search', this.searchTerm.trim());
    if (this.filterStatus)      qs.set('status', this.filterStatus);

    this.http.get<any>(`${environment.apiUrl}/sites?${qs}`).subscribe({
      next: r => {
        this.siteList       = r.items ?? [];
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

  // ── Create ─────────────────────────────────────────────────────────────────
  openCreateModal() {
    this.createForm.reset({ status: 'Pending' });
    this.createError = ''; this.createSuccess = false;
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
    this.createSubmitting = true; this.createError = '';
    const v = this.createForm.value;
    this.http.post(`${environment.apiUrl}/sites`, {
      name: v.name.trim(), location: v.location.trim(), status: v.status
    }).subscribe({
      next: () => {
        this.createSubmitting = false; this.createSuccess = true;
        this.listTotalCount++;
        setTimeout(() => {
          this.showCreateModal = false; this.createSuccess = false;
          this.loadSites(this.listPage);
        }, 1500);
      },
      error: err => {
        this.createSubmitting = false;
        this.createError = err?.error?.error ?? err?.error?.message ?? 'Failed to create site.';
      }
    });
  }

  // ── Edit ───────────────────────────────────────────────────────────────────
  openEditModal(s: any) {
    this.editingItem = s;
    this.editForm.patchValue({ name: s.name, location: s.location, status: s.status });
    this.editError = ''; this.editSuccess = false;
    this.showEditModal = true;
  }

  closeEditModal() {
    if (this.editSubmitting) return;
    this.showEditModal = false; this.editingItem = null;
  }

  submitEdit() {
    if (this.editForm.invalid) { this.editForm.markAllAsTouched(); return; }
    this.editSubmitting = true; this.editError = '';
    const v = this.editForm.value;
    this.http.put<any>(`${environment.apiUrl}/sites/${this.editingItem.siteID}`, {
      name: v.name.trim(), location: v.location.trim(), status: v.status
    }).subscribe({
      next: updated => {
        this.editSubmitting = false; this.editSuccess = true;
        const idx = this.siteList.findIndex(x => x.siteID === this.editingItem.siteID);
        if (idx > -1) this.siteList[idx] = updated;
        setTimeout(() => {
          this.showEditModal = false; this.editSuccess = false; this.editingItem = null;
        }, 1500);
      },
      error: err => {
        this.editSubmitting = false;
        this.editError = err?.error?.error ?? err?.error?.message ?? 'Failed to update site.';
      }
    });
  }

  // ── Delete ─────────────────────────────────────────────────────────────────
  openDeleteConfirm(s: any) {
    this.deletingItem = s; this.deleteError = '';
    this.showDeleteConfirm = true;
  }

  closeDeleteConfirm() {
    if (this.deleteLoading) return;
    this.showDeleteConfirm = false; this.deletingItem = null;
  }

  confirmDelete() {
    if (!this.deletingItem) return;
    this.deleteLoading = true; this.deleteError = '';
    this.http.delete(`${environment.apiUrl}/sites/${this.deletingItem.siteID}`).subscribe({
      next: () => {
        this.deleteLoading = false; this.showDeleteConfirm = false; this.deletingItem = null;
        this.listTotalCount = Math.max(0, this.listTotalCount - 1);
        const newPages = Math.ceil(this.listTotalCount / this.listPageSize) || 1;
        this.loadSites(this.listPage > newPages ? newPages : this.listPage);
      },
      error: err => {
        this.deleteLoading = false;
        this.deleteError = err?.error?.error ?? err?.error?.message ?? 'Failed to delete site.';
      }
    });
  }

  // ── Helpers ────────────────────────────────────────────────────────────────
  statusClass(s: string): string {
    const m: Record<string, string> = {
      Active: 'badge-green', Pending: 'badge-amber',
      Suspended: 'badge-red', Closed: 'badge-slate'
    };
    return m[s] ?? 'badge-slate';
  }

  cf(n: string) { return this.createForm.get(n)!; }
  ef(n: string) { return this.editForm.get(n)!; }

  fieldErr(ctrl: any): string {
    if (!ctrl.touched || ctrl.valid) return '';
    if (ctrl.errors?.['required'])  return 'Required.';
    if (ctrl.errors?.['maxlength']) return `Max ${ctrl.errors['maxlength'].requiredLength} characters.`;
    return 'Invalid value.';
  }

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
  }
}
