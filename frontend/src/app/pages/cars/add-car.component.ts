import {
  Component,
  DestroyRef,
  inject,
  ChangeDetectionStrategy,
  OnInit,
  ChangeDetectorRef,
  ViewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { CarBrandService } from '../../services/car-brand.service';
import { CarModelService } from '../../services/car-model.service';
import { DetailCarService } from '../../services/detail-car.service';
import { CustomerService } from '../../services/customer.service';
import { CarBrand } from '../../models/car-brand';
import { CarModel } from '../../models/car-model';
import { DetailCar } from '../../models/detail-car';
import { Customer } from '../../models/customer';
import { TranslationService } from '../../services/translation.service';
import { ToastService } from '../../services/toast.service';

import { FormsModule, NgForm } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '../../pipes/translate.pipe';
import { markDirty } from '../../utils/mark-dirty';

@Component({
  selector: 'app-add-car',
  imports: [FormsModule, RouterModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [
    `
      .inline-add-form {
        display: flex;
        gap: 6px;
        align-items: center;
        margin-top: 6px;
        flex-wrap: wrap;
      }
      .inline-add-input {
        flex: 1;
        min-width: 120px;
        padding: 4px 8px;
        border: 1px solid var(--border-color, #ccc);
        border-radius: 4px;
        font-size: 0.875rem;
      }
      .btn-sm {
        padding: 4px 10px;
        font-size: 0.8rem;
      }
      .inline-error {
        color: #e74c3c;
        font-size: 0.8rem;
        width: 100%;
      }
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
        <h1>{{ 'cars.addTitle' | translate }}</h1>
        <p>{{ 'cars.addSubtitle' | translate }}</p>
      </div>
      <div class="page-actions">
        <a [routerLink]="'/' + slug + '/cars'" class="btn btn-outline"
          >&larr; {{ 'cars.viewCars' | translate }}</a
        >
      </div>

      @if (errorMessage) {
        <div class="error-message">{{ errorMessage }}</div>
      }
      @if (successMessage) {
        <div class="success-message">
          {{ successMessage }}
        </div>
      }

      <form (ngSubmit)="onSubmit()" #carForm="ngForm" class="inventory-form">
        <div class="form-row">
          <div class="form-group">
            <label for="brand">{{ 'cars.brand' | translate }} *</label>
            <select
              id="brand"
              [(ngModel)]="selectedBrandId"
              name="brand"
              (ngModelChange)="onBrandChange()"
              required
              #brandField="ngModel"
            >
              <option [ngValue]="null">
                -- {{ 'cars.selectBrand' | translate }} --
              </option>
              @for (b of brands; track b.id) {
                <option [ngValue]="b.id">
                  {{ b.brandName }}
                </option>
              }
            </select>
            @if (brandField.invalid && carForm.submitted) {
              <small class="field-error"
                >The Brand field is required, please fill the field to
                continue!</small
              >
            }
            <small class="field-hint">
              <a (click)="toggleAddBrand()" style="cursor:pointer"
                >+ {{ 'cars.addNewBrand' | translate }}</a
              >
            </small>
            @if (showAddBrand) {
              <div class="inline-add-form">
                <input
                  type="text"
                  [(ngModel)]="newBrandName"
                  [placeholder]="'brands.brandName' | translate"
                  name="__newBrand"
                  class="inline-add-input"
                  [disabled]="savingBrand"
                />
                <button
                  type="button"
                  class="btn btn-primary btn-sm"
                  (click)="saveNewBrand()"
                  [disabled]="!newBrandName.trim() || savingBrand"
                >
                  {{
                    savingBrand
                      ? ('common.saving' | translate)
                      : ('common.save' | translate)
                  }}
                </button>
                <button
                  type="button"
                  class="btn btn-outline btn-sm"
                  (click)="toggleAddBrand()"
                >
                  {{ 'common.cancel' | translate }}
                </button>
              </div>
            }
          </div>
          <div class="form-group">
            <label for="carModelId">{{ 'cars.model' | translate }} *</label>
            <select
              id="carModelId"
              [(ngModel)]="newDetail.carModelId"
              name="carModelId"
              required
              [disabled]="!selectedBrandId"
              #carModelField="ngModel"
            >
              <option [ngValue]="null">
                -- {{ 'cars.selectModel' | translate }} --
              </option>
              @for (c of filteredModels; track c.id) {
                <option [ngValue]="c.id">
                  {{ c.modelName }}
                </option>
              }
            </select>
            @if (carModelField.invalid && carForm.submitted) {
              <small class="field-error"
                >The Model field is required, please fill the field to
                continue!</small
              >
            }
            <small class="field-hint">
              <a (click)="toggleAddModel()" style="cursor:pointer"
                >+ {{ 'cars.addNewModel' | translate }}</a
              >
            </small>
            @if (showAddModel) {
              <div class="inline-add-form">
                <input
                  type="text"
                  [(ngModel)]="newModelName"
                  [placeholder]="'models.modelName' | translate"
                  name="__newModel"
                  class="inline-add-input"
                  [disabled]="savingModel"
                />
                <button
                  type="button"
                  class="btn btn-primary btn-sm"
                  (click)="saveNewModel()"
                  [disabled]="
                    !newModelName.trim() || savingModel || !selectedBrandId
                  "
                >
                  {{
                    savingModel
                      ? ('common.saving' | translate)
                      : ('common.save' | translate)
                  }}
                </button>
                <button
                  type="button"
                  class="btn btn-outline btn-sm"
                  (click)="toggleAddModel()"
                >
                  {{ 'common.cancel' | translate }}
                </button>
              </div>
            }
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="customerId">{{ 'cars.customer' | translate }} *</label>
            <select
              id="customerId"
              [(ngModel)]="newDetail.customerId"
              name="customerId"
              required
              #customerIdField="ngModel"
            >
              <option [ngValue]="undefined">
                -- {{ 'cars.selectCustomer' | translate }} --
              </option>
              @for (cu of customers; track cu) {
                <option [ngValue]="cu.id">
                  {{ cu.firstName }} {{ cu.lastName }}
                </option>
              }
            </select>
            @if (customerIdField.invalid && carForm.submitted) {
              <small class="field-error"
                >The Customer field is required, please fill the field to
                continue!</small
              >
            }
            <small class="field-hint">
              <a (click)="toggleAddCustomer()" style="cursor:pointer"
                >+ {{ 'cars.addNewCustomer' | translate }}</a
              >
            </small>
            @if (showAddCustomer) {
              <div class="inline-add-form">
                <input
                  type="text"
                  [(ngModel)]="newCustomerFirstName"
                  [placeholder]="'customers.firstName' | translate"
                  name="__newCustFirst"
                  class="inline-add-input"
                  [disabled]="savingCustomer"
                />
                <input
                  type="text"
                  [(ngModel)]="newCustomerLastName"
                  [placeholder]="'customers.lastName' | translate"
                  name="__newCustLast"
                  class="inline-add-input"
                  [disabled]="savingCustomer"
                />
                <input
                  type="text"
                  [(ngModel)]="newCustomerPhone"
                  [placeholder]="'customers.phone' | translate"
                  name="__newCustPhone"
                  class="inline-add-input"
                  [disabled]="savingCustomer"
                />
                <input
                  type="text"
                  [(ngModel)]="newCustomerIdClient"
                  [placeholder]="'customers.idClient' | translate"
                  name="__newCustIdClient"
                  class="inline-add-input"
                  [disabled]="savingCustomer"
                  (blur)="onNewCustomerIdClientBlur()"
                />
                @if (newCustomerIdClientError) {
                  <small class="inline-error">{{
                    newCustomerIdClientError
                  }}</small>
                }
                <button
                  type="button"
                  class="btn btn-primary btn-sm"
                  (click)="saveNewCustomer()"
                  [disabled]="
                    !newCustomerFirstName.trim() ||
                    !newCustomerLastName.trim() ||
                    !newCustomerPhone.trim() ||
                    savingCustomer ||
                    !!newCustomerIdClientError
                  "
                >
                  {{
                    savingCustomer
                      ? ('common.saving' | translate)
                      : ('common.save' | translate)
                  }}
                </button>
                <button
                  type="button"
                  class="btn btn-outline btn-sm"
                  (click)="toggleAddCustomer()"
                >
                  {{ 'common.cancel' | translate }}
                </button>
              </div>
            }
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="year">{{ 'cars.year' | translate }} *</label>
            <input
              id="year"
              [(ngModel)]="newDetail.year"
              name="year"
              type="number"
              [placeholder]="'cars.yearPlaceholder' | translate"
              required
            />
          </div>
          <div class="form-group">
            <label for="vin">{{ 'cars.vin' | translate }} *</label>
            <input
              id="vin"
              type="text"
              [(ngModel)]="newDetail.vin"
              name="vin"
              [placeholder]="'cars.vinPlaceholder' | translate"
              required
            />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="licensePlate"
              >{{ 'cars.licensePlate' | translate }} *</label
            >
            <input
              id="licensePlate"
              type="text"
              [(ngModel)]="newDetail.licensePlate"
              name="licensePlate"
              [placeholder]="'cars.licensePlatePlaceholder' | translate"
              maxlength="20"
              required
            />
          </div>
          <div class="form-group">
            <label for="mileage">{{ 'cars.mileage' | translate }} *</label>
            <input
              id="mileage"
              type="number"
              [(ngModel)]="newDetail.mileage"
              name="mileage"
              min="0"
              [placeholder]="'cars.mileagePlaceholder' | translate"
              required
            />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="fuel">{{ 'cars.fuel' | translate }} *</label>
            <select id="fuel" [(ngModel)]="newDetail.fuel" name="fuel" required>
              <option value="">
                -- {{ 'cars.selectFuel' | translate }} --
              </option>
              <option value="Gasoline">
                {{ 'cars.fuel.gasoline' | translate }}
              </option>
              <option value="Diesel">
                {{ 'cars.fuel.diesel' | translate }}
              </option>
              <option value="Electric">
                {{ 'cars.fuel.electric' | translate }}
              </option>
              <option value="Hybrid">
                {{ 'cars.fuel.hybrid' | translate }}
              </option>
              <option value="LPG">{{ 'cars.fuel.lpg' | translate }}</option>
            </select>
          </div>
          <div class="form-group">
            <label for="typeCar">{{ 'cars.typeCar' | translate }} *</label>
            <select
              id="typeCar"
              [(ngModel)]="newDetail.typeCar"
              name="typeCar"
              required
            >
              <option value="">
                -- {{ 'cars.selectType' | translate }} --
              </option>
              <option value="Sedan">Sedan</option>
              <option value="SUV">SUV</option>
              <option value="Hatchback">Hatchback</option>
              <option value="Coupe">Coupe</option>
              <option value="Truck">Truck</option>
              <option value="Van">Van</option>
              <option value="Convertible">Convertible</option>
              <option value="Wagon">Wagon</option>
            </select>
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="transmissionType"
              >{{ 'cars.transmission' | translate }} *</label
            >
            <select
              id="transmissionType"
              [(ngModel)]="newDetail.transmissionType"
              name="transmissionType"
              required
            >
              <option value="">
                -- {{ 'cars.selectTransmission' | translate }} --
              </option>
              <option value="Automatic">
                {{ 'cars.trans.automatic' | translate }}
              </option>
              <option value="Manual">
                {{ 'cars.trans.manual' | translate }}
              </option>
              <option value="CVT">CVT</option>
              <option value="DCT">DCT</option>
            </select>
          </div>
        </div>

        <button type="submit" class="btn btn-primary" [disabled]="submitting">
          {{
            submitting
              ? ('common.adding' | translate)
              : ('cars.addBtn' | translate)
          }}
        </button>
      </form>
    </div>
  `,
})
export class AddCarComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private cdr = inject(ChangeDetectorRef);
  @ViewChild('carForm') carForm!: NgForm;
  errorMessage = '';
  successMessage = '';
  submitting = false;
  brands: CarBrand[] = [];
  customers: Customer[] = [];
  filteredModels: CarModel[] = [];
  selectedBrandId: number | null = null;
  showAddBrand = false;
  showAddModel = false;
  showAddCustomer = false;
  newBrandName = '';
  newModelName = '';
  newCustomerFirstName = '';
  newCustomerLastName = '';
  newCustomerPhone = '';
  newCustomerIdClient = '';
  newCustomerIdClientError = '';
  savingBrand = false;
  savingModel = false;
  savingCustomer = false;
  newDetail: DetailCar = {
    carModelId: null as any,
    customerId: undefined,
    vin: '',
    fuel: '',
    year: new Date().getFullYear(),
    typeCar: '',
    transmissionType: '',
    licensePlate: '',
    mileage: 0,
  };

  constructor(
    private brandService: CarBrandService,
    private modelService: CarModelService,
    private detailCarService: DetailCarService,
    private customerService: CustomerService,
    private router: Router,
    public ts: TranslationService,
    private toast: ToastService,
  ) {}

  get slug(): string {
    return this.router.url.split('/').filter(Boolean)[0] || '';
  }

  ngOnInit(): void {
    this.brandService
      .getBrands()
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe((data) => {
        this.brands = data;
      });
    this.customerService
      .getCustomers()
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe((data) => (this.customers = data));
  }

  onBrandChange(): void {
    this.filteredModels = [];
    this.newDetail.carModelId = null as any;
    if (this.selectedBrandId) {
      this.modelService
        .getModelsByBrand(this.selectedBrandId)
        .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
        .subscribe((data) => {
          this.filteredModels = data;
        });
    }
  }

  toggleAddBrand(): void {
    this.showAddBrand = !this.showAddBrand;
    this.newBrandName = '';
  }

  toggleAddModel(): void {
    this.showAddModel = !this.showAddModel;
    this.newModelName = '';
  }

  saveNewBrand(): void {
    const name = this.newBrandName.trim();
    if (!name) return;
    this.savingBrand = true;
    this.brandService
      .addBrand({ brandName: name })
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe({
        next: () => {
          this.savingBrand = false;
          this.showAddBrand = false;
          this.newBrandName = '';
          this.brandService
            .getBrands()
            .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
            .subscribe((data) => {
              this.brands = data;
              const added = data.find((b) => b.brandName === name);
              if (added) {
                this.selectedBrandId = added.id!;
                this.onBrandChange();
              }
            });
        },
        error: () => {
          this.savingBrand = false;
          this.toast.error(this.ts.t('common.updateError'));
        },
      });
  }

  saveNewModel(): void {
    const name = this.newModelName.trim();
    if (!name || !this.selectedBrandId) return;
    this.savingModel = true;
    this.modelService
      .addModel({ brandId: this.selectedBrandId, modelName: name })
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe({
        next: () => {
          this.savingModel = false;
          this.showAddModel = false;
          this.newModelName = '';
          this.modelService
            .getModelsByBrand(this.selectedBrandId!)
            .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
            .subscribe((data) => {
              this.filteredModels = data;
              const added = data.find((m) => m.modelName === name);
              if (added) this.newDetail.carModelId = added.id!;
            });
        },
        error: () => {
          this.savingModel = false;
          this.toast.error(this.ts.t('common.updateError'));
        },
      });
  }

  refreshBrands(): void {
    this.brandService
      .getBrands()
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe((data) => {
        this.brands = data;
        if (this.selectedBrandId) this.onBrandChange();
      });
  }

  toggleAddCustomer(): void {
    this.showAddCustomer = !this.showAddCustomer;
    this.newCustomerFirstName = '';
    this.newCustomerLastName = '';
    this.newCustomerPhone = '';
    this.newCustomerIdClient = '';
    this.newCustomerIdClientError = '';
  }

  onNewCustomerIdClientBlur(): void {
    const idClient = this.newCustomerIdClient.trim();
    if (!idClient) {
      this.newCustomerIdClientError = '';
      return;
    }
    this.customerService
      .checkIdClient(idClient)
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe({
        next: (res) => {
          this.newCustomerIdClientError = res.exists
            ? this.ts.t('customers.error.idClientExists')
            : '';
        },
        error: () => {
          this.newCustomerIdClientError = '';
        },
      });
  }

  saveNewCustomer(): void {
    const firstName = this.newCustomerFirstName.trim();
    const lastName = this.newCustomerLastName.trim();
    const phone = this.newCustomerPhone.trim();
    const idClient = this.newCustomerIdClient.trim();
    if (!firstName || !lastName || !phone) return;
    if (this.newCustomerIdClientError) return;
    this.savingCustomer = true;
    this.customerService
      .addCustomer({
        firstName,
        lastName,
        phoneNumber: phone,
        idClient: idClient || undefined,
      })
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe({
        next: () => {
          this.savingCustomer = false;
          this.showAddCustomer = false;
          this.newCustomerFirstName = '';
          this.newCustomerLastName = '';
          this.newCustomerPhone = '';
          this.newCustomerIdClient = '';
          this.newCustomerIdClientError = '';
          this.customerService
            .getCustomers()
            .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
            .subscribe((data) => {
              this.customers = data;
              const added = data.find(
                (c) => c.firstName === firstName && c.lastName === lastName,
              );
              if (added) this.newDetail.customerId = added.id;
            });
        },
        error: (err) => {
          this.savingCustomer = false;
          if (err.status === 409) {
            this.newCustomerIdClientError = this.ts.t(
              'customers.error.idClientExists',
            );
          } else {
            this.toast.error(this.ts.t('customers.error.add'));
          }
        },
      });
  }

  onSubmit(): void {
    if (
      !this.newDetail.carModelId ||
      !this.newDetail.customerId ||
      !this.newDetail.vin ||
      !this.newDetail.licensePlate ||
      this.newDetail.mileage === undefined ||
      this.newDetail.mileage === null ||
      this.newDetail.mileage < 0 ||
      !this.newDetail.fuel ||
      !this.newDetail.typeCar ||
      !this.newDetail.transmissionType
    ) {
      this.toast.error(this.ts.t('common.fieldsRequired'));
      return;
    }
    this.submitting = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.detailCarService
      .addDetailCar(this.newDetail)
      .pipe(takeUntilDestroyed(this.destroyRef), markDirty(this.cdr))
      .subscribe({
        next: () => {
          const model = this.filteredModels.find(
            (c) => c.id === this.newDetail.carModelId,
          );
          const brand = this.brands.find((b) => b.id === this.selectedBrandId);
          this.successMessage = this.ts.t('cars.success.add', {
            brand: brand?.brandName || '',
            model: model?.modelName || '',
            year: String(this.newDetail.year),
          });
          this.carForm.resetForm();
          this.newDetail = {
            carModelId: null as any,
            customerId: undefined,
            vin: '',
            fuel: '',
            year: new Date().getFullYear(),
            typeCar: '',
            transmissionType: '',
            licensePlate: '',
            mileage: 0,
          };
          this.selectedBrandId = null;
          this.filteredModels = [];
          this.submitting = false;
        },
        error: () => {
          this.errorMessage = this.ts.t('cars.error.add');
          this.submitting = false;
        },
      });
  }
}
