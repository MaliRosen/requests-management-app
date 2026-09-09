import {
  Component,
  EventEmitter,
  OnDestroy,
  OnInit,
  Output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';

import { RequestStatus, RequestType } from '../../models/request.model';
import { SearchQuery } from '../../models/search-query.model';

interface EnumOption<T> {
  label: string;
  value: T;
}

@Component({
  selector: 'app-search-filter',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './search-filter.component.html',
  styleUrls: ['./search-filter.component.scss'],
})
export class SearchFilterComponent implements OnInit, OnDestroy {
  @Output() filterChange = new EventEmitter<Partial<SearchQuery>>();
  @Output() validationError = new EventEmitter<boolean>();

  form!: FormGroup;

  readonly statusOptions: EnumOption<RequestStatus>[] = [
    { label: 'חדש',   value: RequestStatus.New },
    { label: 'בטיפול', value: RequestStatus.InProgress },
    { label: 'הושלם',  value: RequestStatus.Completed },
    { label: 'בוטל',   value: RequestStatus.Cancelled },
  ];

  readonly requestTypeOptions: EnumOption<RequestType>[] = [
    { label: 'כללי',   value: RequestType.General },
    { label: 'משפטי',  value: RequestType.Legal },
    { label: 'תשלום',  value: RequestType.Payment },
    { label: 'ערר',    value: RequestType.Appeal },
  ];

  selectedStatuses = new Set<RequestStatus>();
  selectedTypes = new Set<RequestType>();
  dateRangeError: string | null = null;

  private readonly destroy$ = new Subject<void>();

  constructor(private fb: FormBuilder) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      requestNumber: [''],
      createdFrom: [''],
      createdTo: [''],
    });

    this.form.get('requestNumber')!
      .valueChanges.pipe(
        debounceTime(500),
        distinctUntilChanged(),
        takeUntil(this.destroy$),
      )
      .subscribe(() => this.emitFilter());

    // האזנה לשני שדות התאריך — כל שינוי בכל אחד מהם מפעיל ולידציה מיידית
    this.form.get('createdFrom')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.validateAndEmit());

    this.form.get('createdTo')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.validateAndEmit());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onStatusChange(value: RequestStatus, checked: boolean): void {
    checked ? this.selectedStatuses.add(value) : this.selectedStatuses.delete(value);
    this.emitFilter();
  }

  onTypeChange(value: RequestType, checked: boolean): void {
    checked ? this.selectedTypes.add(value) : this.selectedTypes.delete(value);
    this.emitFilter();
  }

  isStatusChecked(value: RequestStatus): boolean {
    return this.selectedStatuses.has(value);
  }

  isTypeChecked(value: RequestType): boolean {
    return this.selectedTypes.has(value);
  }

  clearFilters(): void {
    this.form.reset({ requestNumber: '', createdFrom: '', createdTo: '' });
    this.selectedStatuses.clear();
    this.selectedTypes.clear();
    this.dateRangeError = null;
    this.filterChange.emit({});
  }

  private validateAndEmit(): void {
    this.dateRangeError = null;
    this.emitFilter(); // תמיד פולטים — app.ts מחליט אם לשלוח לשרת
  }

  private emitFilter(): void {
    const raw = this.form.value;
    // שולחים את המצב המלא של הטופס בכל שינוי
    // כך query ב-AppComponent תמיד מייצג את מה שרואים בטופס
    const filter: Partial<SearchQuery> = {};

    if (raw.requestNumber) {
      filter.requestNumber = raw.requestNumber as string;
    }
    if (this.selectedStatuses.size > 0) {
      filter.status = Array.from(this.selectedStatuses);
    }
    if (this.selectedTypes.size > 0) {
      filter.requestType = Array.from(this.selectedTypes);
    }
    if (raw.createdFrom) {
      filter.createdFrom = raw.createdFrom;
    }
    if (raw.createdTo) {
      filter.createdTo = raw.createdTo;
    }

    console.log('emitFilter:', JSON.stringify(filter));
    this.filterChange.emit(filter);
  }
}
