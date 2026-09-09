import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RequestDto, RequestStatus, RequestType } from '../../models/request.model';

@Component({
  selector: 'app-requests-table',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './requests-table.component.html',
  styleUrls: ['./requests-table.component.scss']
})
export class RequestsTableComponent {
  @Input() items: RequestDto[] = [];
  @Input() sortBy: string = 'CreatedAt';
  @Input() sortDirection: string = 'desc';

  @Output() sortChange = new EventEmitter<{ sortBy: string; sortDirection: string }>();

  readonly columns = [
    { key: 'Id',            label: 'מזהה' },
    { key: 'RequestNumber', label: 'מספר בקשה' },
    { key: 'Status',        label: 'סטטוס' },
    { key: 'RequestType',   label: 'סוג בקשה' },
    { key: 'CreatedAt',     label: 'תאריך יצירה' },
    { key: 'OwnerId',       label: 'בעלים' },
  ];

  readonly statusLabels: Record<number, string> = {
    [RequestStatus.New]:        'חדש',
    [RequestStatus.InProgress]: 'בטיפול',
    [RequestStatus.Completed]:  'הושלם',
    [RequestStatus.Cancelled]:  'בוטל',
  };

  readonly typeLabels: Record<number, string> = {
    [RequestType.General]: 'כללי',
    [RequestType.Legal]:   'משפטי',
    [RequestType.Payment]: 'תשלום',
    [RequestType.Appeal]:  'ערר',
  };

  onSort(column: string): void {
    if (column === this.sortBy) {
      // לחיצה חוזרת על אותה עמודה — הפוך כיוון
      const newDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
      this.sortChange.emit({ sortBy: column, sortDirection: newDirection });
    } else {
      // עמודה חדשה — ברירת מחדל desc (החדש/הגדול קודם)
      this.sortChange.emit({ sortBy: column, sortDirection: 'desc' });
    }
  }

  getSortIndicator(column: string): string {
    if (column !== this.sortBy) return '';
    return this.sortDirection === 'asc' ? '▲' : '▼';
  }
}
