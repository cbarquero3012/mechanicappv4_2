import {
  Component,
  DestroyRef,
  inject,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  computed,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RepairOrderService } from '../../services/repair-order.service';
import { RepairOrder } from '../../models/repair-order';
import { TranslationService } from '../../services/translation.service';
import { ToastService } from '../../services/toast.service';
import { AuthService } from '../../services/auth.service';
import { DecimalPipe } from '@angular/common';
import { LocalDatePipe } from '../../pipes/local-date.pipe';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '../../pipes/translate.pipe';
import { StatusLabelPipe } from '../../pipes/status-label.pipe';
@Component({
  selector: 'app-view-repair-orders',
  imports: [
    FormsModule,
    RouterModule,
    LocalDatePipe,
    DecimalPipe,
    TranslatePipe,
    StatusLabelPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="module-page">
      <div class="page-header">
        <h1>&#128736; {{ 'orders.title' | translate }}</h1>
        <p>{{ 'orders.count' | translate: { count: orders().length } }}</p>
      </div>
      <div class="page-actions">
        <a routerLink="../repair-orders/add" class="btn btn-primary"
          >+ {{ 'orders.new' | translate }}</a
        >
      </div>

      <div class="filter-bar">
        <input
          type="text"
          [ngModel]="searchTerm()"
          (ngModelChange)="searchTerm.set($event)"
          [placeholder]="'orders.search' | translate"
          class="search-input"
        />
        <select
          [ngModel]="filterStatus()"
          (ngModelChange)="filterStatus.set($event)"
          class="filter-select"
        >
          <option value="">{{ 'orders.allStatuses' | translate }}</option>
          <option value="Pending">{{ 'status.pending' | translate }}</option>
          <option value="In Progress">
            {{ 'status.inProgress' | translate }}
          </option>
          <option value="Completed">
            {{ 'status.completed' | translate }}
          </option>
          <option value="Cancelled">
            {{ 'status.cancelled' | translate }}
          </option>
        </select>
      </div>

      @if (errorMsg()) {
        <div class="error-message">{{ errorMsg() }}</div>
      }
      @if (successMsg()) {
        <div class="success-message">{{ successMsg() }}</div>
      }

      @if (filteredOrders().length > 0) {
        <div class="inventory-table-wrapper">
          <table class="inventory-table">
            <thead>
              <tr>
                <th>#</th>
                <th>{{ 'orders.vehicle' | translate }}</th>
                <th>{{ 'orders.client' | translate }}</th>
                <th>{{ 'orders.plate' | translate }}</th>
                <th>{{ 'orders.date' | translate }}</th>
                <th>{{ 'common.status' | translate }}</th>
                @if (authService.canSeePrices) {
                  <th>{{ 'orders.total' | translate }}</th>
                }
                <th>{{ 'orders.notes' | translate }}</th>
                <th>{{ 'common.actions' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (o of filteredOrders(); track o.id) {
                <tr>
                  @if (editingId() !== o.id) {
                    <td>{{ o.id }}</td>
                    <td>{{ o.carInfo || 'N/A' }}</td>
                    <td>{{ o.customerName || '-' }}</td>
                    <td>{{ o.licensePlate || '-' }}</td>
                    <td>{{ o.orderDate | localDate: 'short' }}</td>
                    <td>
                      <span
                        class="order-status"
                        [class.status-pending]="o.status === 'Pending'"
                        [class.status-progress]="o.status === 'In Progress'"
                        [class.status-completed]="o.status === 'Completed'"
                        [class.status-cancelled]="o.status === 'Cancelled'"
                      >
                        {{ o.status | statusLabel }}
                      </span>
                    </td>
                    @if (authService.canSeePrices) {
                      <td>
                        {{ o.currencySymbol || '₡'
                        }}{{ o.totalCost | number: '1.2-2' }}
                      </td>
                    }
                    <td class="notes-cell" [title]="o.notes || ''">
                      {{ o.notes || '-' }}
                    </td>
                    <td>
                      @if (authService.isAdmin) {
                        <button
                          class="btn-icon"
                          (click)="startEdit(o)"
                          title="Edit"
                        >
                          &#9998;
                        </button>
                      }
                      <a
                        class="btn-icon"
                        [routerLink]="['../repair-orders', o.id]"
                        title="View Details"
                        >&#128269;</a
                      >
                      <button
                        class="btn-icon btn-delete"
                        (click)="deleteOrder(o.id!)"
                      >
                        &#128465;
                      </button>
                    </td>
                  }
                  @if (editingId() === o.id) {
                    <td>{{ o.id }}</td>
                    <td>{{ o.carInfo || 'N/A' }}</td>
                    <td>{{ o.customerName || '-' }}</td>
                    <td>{{ o.licensePlate || '-' }}</td>
                    <td>{{ o.orderDate | localDate: 'short' }}</td>
                    <td>
                      <select
                        [(ngModel)]="editItem.status"
                        class="inline-edit-input"
                      >
                        <option value="Pending">
                          {{ 'status.pending' | translate }}
                        </option>
                        <option value="In Progress">
                          {{ 'status.inProgress' | translate }}
                        </option>
                        <option value="Completed">
                          {{ 'status.completed' | translate }}
                        </option>
                        <option value="Cancelled">
                          {{ 'status.cancelled' | translate }}
                        </option>
                      </select>
                    </td>
                    @if (authService.canSeePrices) {
                      <td>
                        {{ o.currencySymbol || '₡'
                        }}{{ o.totalCost | number: '1.2-2' }}
                      </td>
                    }
                    <td>
                      <input
                        type="text"
                        [(ngModel)]="editItem.notes"
                        class="inline-edit-input"
                      />
                    </td>
                    <td>
                      <button
                        class="btn-icon btn-save"
                        (click)="saveEdit()"
                        title="Save"
                      >
                        &#128190;
                      </button>
                      <button
                        class="btn-icon"
                        (click)="cancelEdit()"
                        title="Cancel"
                      >
                        &#10060;
                      </button>
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (filteredOrders().length === 0 && !loading()) {
        <div class="empty-state">
          <p>
            {{ 'orders.empty' | translate }}
            <a routerLink="../repair-orders/add">{{
              'orders.createFirst' | translate
            }}</a
            >.
          </p>
        </div>
      }
      @if (loading()) {
        <div class="loading">
          {{ 'orders.loading' | translate }}
        </div>
      }
    </div>
  `,
})
export class ViewRepairOrdersComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  orders = signal<RepairOrder[]>([]);
  loading = signal(true);
  searchTerm = signal('');
  filterStatus = signal('');
  editingId = signal<number | null>(null);
  editItem: RepairOrder = { status: 'Pending', totalCost: 0 };
  errorMsg = signal('');
  successMsg = signal('');

  filteredOrders = computed(() =>
    this.orders().filter((o) => {
      const term = this.searchTerm().toLowerCase();
      const matchSearch =
        !term ||
        (o.carInfo || '').toLowerCase().includes(term) ||
        (o.mechanicName || '').toLowerCase().includes(term) ||
        (o.notes || '').toLowerCase().includes(term) ||
        (o.customerName || '').toLowerCase().includes(term) ||
        (o.licensePlate || '').toLowerCase().includes(term);
      const matchStatus =
        !this.filterStatus() || o.status === this.filterStatus();
      return matchSearch && matchStatus;
    }),
  );

  constructor(
    private orderService: RepairOrderService,
    public ts: TranslationService,
    private toast: ToastService,
    public authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.loading.set(true);
    this.orderService
      .getOrders()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.orders.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  startEdit(o: RepairOrder): void {
    this.editingId.set(o.id!);
    this.editItem = { ...o };
    this.clearMessages();
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveEdit(): void {
    if (!this.editItem.status?.trim()) {
      this.toast.error(this.ts.t('common.fieldsRequired'));
      return;
    }
    this.orderService
      .updateOrder(this.editItem)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.editingId.set(null);
          this.successMsg.set(this.ts.t('common.updateSuccess'));
          this.loadOrders();
        },
        error: () => {
          this.errorMsg.set(this.ts.t('common.updateError'));
        },
      });
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Pending: this.ts.t('status.pending'),
      'In Progress': this.ts.t('status.inProgress'),
      Completed: this.ts.t('status.completed'),
      Cancelled: this.ts.t('status.cancelled'),
    };
    return map[status] || status;
  }

  deleteOrder(id: number): void {
    if (!confirm(this.ts.t('orders.confirmDelete'))) return;
    this.clearMessages();
    this.orderService
      .deleteOrder(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadOrders(),
        error: () => {
          this.errorMsg.set(this.ts.t('common.deleteError'));
        },
      });
  }

  private clearMessages(): void {
    this.errorMsg.set('');
    this.successMsg.set('');
  }
}
