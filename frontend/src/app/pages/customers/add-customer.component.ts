import {
  Component,
  ChangeDetectionStrategy,
  ViewChild,
  DestroyRef,
  inject,
  ChangeDetectorRef,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgForm } from '@angular/forms';
import { CustomerService } from '../../services/customer.service';
import { Customer } from '../../models/customer';
import { TranslationService } from '../../services/translation.service';
import { ToastService } from '../../services/toast.service';

import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { TranslatePipe } from '../../pipes/translate.pipe';
import { markDirty } from '../../utils/mark-dirty';

@Component({
  selector: 'app-add-customer',
  imports: [FormsModule, RouterModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [
    `
      .field-error {
        color: #e74c3c;
        font-size: 0.8rem;
        margin-top: 4px;
      }
    `,
  ],
  template: `
    <div class="module-page">
      <div class="page-header">
        <h1>+ {{ 'customers.addTitle' | translate }}</h1>
        <p>{{ 'customers.addSubtitle' | translate }}</p>
      </div>
      <div class="page-actions">
        <a [routerLink]="'/' + slug + '/customers'" class="btn btn-outline"
          >&larr; {{ 'customers.viewCustomers' | translate }}</a
        >
      </div>

      <form
        (ngSubmit)="onSubmit()"
        #customerForm="ngForm"
        class="inventory-form"
      >
        @if (errorMsg) {
          <div class="error-message">{{ errorMsg }}</div>
        }
        @if (idClientExistsMsg) {
          <div class="info-message">{{ idClientExistsMsg }}</div>
        }
        <div class="form-row">
          <div class="form-group">
            <label for="firstName"
              >{{ 'customers.firstName' | translate }} *</label
            >
            <input
              id="firstName"
              type="text"
              [(ngModel)]="customer.firstName"
              name="firstName"
              required
              #firstNameField="ngModel"
            />
            @if (firstNameField.invalid && customerForm.submitted) {
              <small class="field-error"
                >The First Name field is required, please fill the field to
                continue!</small
              >
            }
          </div>
          <div class="form-group">
            <label for="lastName"
              >{{ 'customers.lastName' | translate }} *</label
            >
            <input
              id="lastName"
              type="text"
              [(ngModel)]="customer.lastName"
              name="lastName"
              required
              #lastNameField="ngModel"
            />
            @if (lastNameField.invalid && customerForm.submitted) {
              <small class="field-error"
                >The Last Name field is required, please fill the field to
                continue!</small
              >
            }
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="email">{{ 'customers.email' | translate }}</label>
            <input
              id="email"
              type="email"
              [(ngModel)]="customer.email"
              name="email"
            />
          </div>
          <div class="form-group">
            <label for="phoneNumber"
              >{{ 'customers.phone' | translate }} *</label
            >
            <input
              id="phoneNumber"
              type="text"
              [(ngModel)]="customer.phoneNumber"
              name="phoneNumber"
              required
              #phoneField="ngModel"
            />
            @if (phoneField.invalid && customerForm.submitted) {
              <small class="field-error"
                >The Phone field is required, please fill the field to
                continue!</small
              >
            }
          </div>
        </div>

        <div class="form-row">
          <div class="form-group full-width">
            <label for="address">{{ 'customers.address' | translate }}</label>
            <input
              id="address"
              type="text"
              [(ngModel)]="customer.address"
              name="address"
            />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="idClient"
              >{{ 'customers.idClient' | translate }} *</label
            >
            <input
              id="idClient"
              type="text"
              [(ngModel)]="customer.idClient"
              name="idClient"
              (blur)="onIdClientBlur()"
              required
              #idClientField="ngModel"
            />
            @if (idClientField.invalid && customerForm.submitted) {
              <small class="field-error"
                >The Client ID field is required, please fill the field to
                continue!</small
              >
            }
          </div>
          <div class="form-group">
            <label for="economicActivityCode">{{
              'customers.economicActivityCode' | translate
            }}</label>
            <input
              id="economicActivityCode"
              type="text"
              [(ngModel)]="customer.economicActivityCode"
              name="economicActivityCode"
            />
            <small class="field-hint">{{
              'customers.economicActivityCodeHint' | translate
            }}</small>
          </div>
        </div>

        <button
          type="submit"
          class="btn btn-primary"
          [disabled]="!!idClientExistsMsg"
        >
          {{ 'customers.save' | translate }}
        </button>
      </form>

      @if (successMsg) {
        <div class="success-message">{{ successMsg }}</div>
      }
    </div>
  `,
})
export class AddCustomerComponent {
  private destroyRef = inject(DestroyRef);
  private cdr = inject(ChangeDetectorRef);
  @ViewChild('customerForm') customerForm!: NgForm;
  customer: Customer = { firstName: '', lastName: '', phoneNumber: '' };
  successMsg = '';
  errorMsg = '';
  idClientExistsMsg = '';

  constructor(
    private customerService: CustomerService,
    public ts: TranslationService,
    private toast: ToastService,
    private router: Router,
  ) {}

  get slug(): string {
    return this.router.url.split('/').filter(Boolean)[0] || '';
  }

  onIdClientBlur(): void {
    const idClient = this.customer.idClient?.trim();
    if (!idClient) {
      this.idClientExistsMsg = '';
      return;
    }
    this.customerService
      .checkIdClient(idClient)
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe({
        next: (res) => {
          this.idClientExistsMsg = res.exists
            ? this.ts.t('customers.error.idClientExists')
            : '';
        },
        error: () => {
          this.idClientExistsMsg = '';
        },
      });
  }

  onSubmit(): void {
    if (this.customerForm?.invalid) {
      this.toast.error(this.ts.t('common.fieldsRequired'));
      return;
    }
    if (this.idClientExistsMsg) {
      return;
    }
    this.errorMsg = '';
    this.customerService
      .addCustomer(this.customer)
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe({
        next: () => {
          this.successMsg = this.ts.t('customers.success.add', {
            name: this.customer.firstName + ' ' + this.customer.lastName,
          });
          this.customerForm.resetForm();
          this.customer = { firstName: '', lastName: '', phoneNumber: '' };
          this.idClientExistsMsg = '';
          setTimeout(() => (this.successMsg = ''), 3000);
        },
        error: (err) => {
          if (err.status === 409) {
            this.idClientExistsMsg = this.ts.t(
              'customers.error.idClientExists',
            );
          } else {
            this.errorMsg = this.ts.t('customers.error.add');
          }
        },
      });
  }
}
