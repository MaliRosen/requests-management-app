import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
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

  // 2 — שינוי requestNumber פולט filterChange אחרי debounce של 500ms
  it('should emit filterChange after 500ms debounce on requestNumber change', fakeAsync(() => {
    const emitted: any[] = [];
    component.filterChange.subscribe(f => emitted.push(f));

    component.form.get('requestNumber')!.setValue('REQ');
    tick(499);
    expect(emitted.length).toBe(0); // טרם עבר הdebounce

    tick(1);
    expect(emitted.length).toBe(1);
    expect(emitted[0].requestNumber).toBe('REQ');
  }));

  // 3 — סימון status מוסיף אותו לסט ופולט filterChange
  it('should add status to selectedStatuses and emit filterChange on check', () => {
    const emitted: any[] = [];
    component.filterChange.subscribe(f => emitted.push(f));

    component.onStatusChange(RequestStatus.New, true);

    expect(component.selectedStatuses.has(RequestStatus.New)).toBeTrue();
    expect(emitted.length).toBe(1);
    expect(emitted[0].status).toContain(RequestStatus.New);
  });

  // 4 — ביטול סימון status מסיר אותו מהסט
  it('should remove status from selectedStatuses on uncheck', () => {
    component.onStatusChange(RequestStatus.New, true);
    component.onStatusChange(RequestStatus.New, false);

    expect(component.selectedStatuses.has(RequestStatus.New)).toBeFalse();
  });

  // 5 — תאריך "עד" לפני "מתאריך" פולט שגיאת ולידציה ולא פולט filterChange
  it('should emit validationError=true when createdTo is before createdFrom', () => {
    const errors: boolean[] = [];
    const filters: any[] = [];
    component.validationError.subscribe(e => errors.push(e));
    component.filterChange.subscribe(f => filters.push(f));

    component.form.get('createdFrom')!.setValue('2024-06-10');
    component.form.get('createdTo')!.setValue('2024-06-01'); // לפני createdFrom

    expect(errors).toContain(true);
    expect(component.dateRangeError).toBeTruthy();
    // filterChange לא אמור להיפלט כשיש שגיאת ולידציה
    const invalidEmits = filters.filter(f => f.createdTo === '2024-06-01');
    expect(invalidEmits.length).toBe(0);
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
});
