import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { SearchFilterComponent } from './search-filter.component';
import { RequestStatus, RequestType } from '../../models/request.model';

describe('SearchFilterComponent', () => {
  let component: SearchFilterComponent;
  let fixture: ComponentFixture<SearchFilterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SearchFilterComponent, ReactiveFormsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(SearchFilterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // 1 — טופס נוצר עם ערכי ברירת מחדל ריקים
  it('should initialise form with empty values', () => {
    expect(component.form.value).toEqual({
      requestNumber: '',
      createdFrom: '',
      createdTo: '',
    });
    expect(component.selectedStatuses.size).toBe(0);
    expect(component.selectedTypes.size).toBe(0);
  });

  // 2 — לחיצה על חפש פולטת filterChange עם הערכים הנוכחיים
  it('should emit filterChange on search() call', () => {
    const emitted: any[] = [];
    component.filterChange.subscribe(f => emitted.push(f));

    component.form.get('requestNumber')!.setValue('REQ');
    component.search();

    expect(emitted.length).toBe(1);
    expect(emitted[0].requestNumber).toBe('REQ');
  });

  // 3 — סימון status מוסיף אותו לסט (ללא פליטה — רק בלחיצה על חפש)
  it('should add status to selectedStatuses on check', () => {
    component.onStatusChange(RequestStatus.New, true);
    expect(component.selectedStatuses.has(RequestStatus.New)).toBeTrue();
  });

  // 4 — ביטול סימון status מסיר אותו מהסט
  it('should remove status from selectedStatuses on uncheck', () => {
    component.onStatusChange(RequestStatus.New, true);
    component.onStatusChange(RequestStatus.New, false);
    expect(component.selectedStatuses.has(RequestStatus.New)).toBeFalse();
  });

  // 5 — תאריך "עד" לפני "מתאריך" פולט שגיאת ולידציה ולא פולט filterChange
  it('should emit validationError=true and not emit filterChange when createdTo is before createdFrom', () => {
    const errors: boolean[] = [];
    const filters: any[] = [];
    component.validationError.subscribe(e => errors.push(e));
    component.filterChange.subscribe(f => filters.push(f));

    component.form.get('createdFrom')!.setValue('2024-06-10');
    component.form.get('createdTo')!.setValue('2024-06-01');
    component.search();

    expect(errors).toContain(true);
    expect(component.dateRangeError).toBeTruthy();
    expect(filters.length).toBe(0);
  });

  // 6 — clearFilters מאפס את כל השדות ופולט filterChange עם אובייקט ריק
  it('should reset all fields and emit empty filter on clearFilters', () => {
    const emitted: any[] = [];
    component.filterChange.subscribe(f => emitted.push(f));

    component.onStatusChange(RequestStatus.New, true);
    component.onTypeChange(RequestType.Legal, true);
    component.form.get('requestNumber')!.setValue('test');

    component.clearFilters();

    expect(component.selectedStatuses.size).toBe(0);
    expect(component.selectedTypes.size).toBe(0);
    expect(component.form.value.requestNumber).toBe('');
    expect(component.dateRangeError).toBeNull();

    const lastEmit = emitted[emitted.length - 1];
    expect(lastEmit).toEqual({});
  });

  // 7 — תאריכים שווים (גבול) — תקין, לא שגיאה
  it('should not emit validationError when createdFrom equals createdTo', () => {
    const errors: boolean[] = [];
    const filters: any[] = [];
    component.validationError.subscribe(e => errors.push(e));
    component.filterChange.subscribe(f => filters.push(f));

    component.form.get('createdFrom')!.setValue('2024-06-10');
    component.form.get('createdTo')!.setValue('2024-06-10');
    component.search();

    expect(component.dateRangeError).toBeNull();
    expect(filters.length).toBe(1);
    expect(errors).not.toContain(true);
  });

  // 8 — סינון משולב — status + requestType + requestNumber נפלטים ביחד
  it('should emit combined filter with status, requestType and requestNumber', () => {
    const emitted: any[] = [];
    component.filterChange.subscribe(f => emitted.push(f));

    component.form.get('requestNumber')!.setValue('REQ-001');
    component.onStatusChange(RequestStatus.New, true);
    component.onStatusChange(RequestStatus.InProgress, true);
    component.onTypeChange(RequestType.Legal, true);
    component.search();

    expect(emitted.length).toBe(1);
    expect(emitted[0].requestNumber).toBe('REQ-001');
    expect(emitted[0].status).toContain(RequestStatus.New);
    expect(emitted[0].status).toContain(RequestStatus.InProgress);
    expect(emitted[0].requestType).toContain(RequestType.Legal);
  });

  // 9 — אחרי שגיאת ולידציה, תיקון התאריך ולחיצת חפש מנקים את השגיאה
  it('should clear validationError after fixing date range and calling search', () => {
    component.form.get('createdFrom')!.setValue('2024-06-10');
    component.form.get('createdTo')!.setValue('2024-06-01');
    component.search();
    expect(component.dateRangeError).toBeTruthy();

    component.form.get('createdTo')!.setValue('2024-06-15');
    component.search();
    expect(component.dateRangeError).toBeNull();
  });

  // 10 — search עם טופס ריק פולט אובייקט ריק
  it('should emit empty filter object when form is empty', () => {
    const emitted: any[] = [];
    component.filterChange.subscribe(f => emitted.push(f));

    component.search();

    expect(emitted.length).toBe(1);
    expect(emitted[0]).toEqual({});
  });
});
