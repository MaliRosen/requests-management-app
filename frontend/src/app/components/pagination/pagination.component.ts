import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pagination.component.html',
  styleUrls: ['./pagination.component.scss']
})
export class PaginationComponent {
  @Input() page: number = 1;
  @Input() pageSize: number = 20;
  @Input() totalCount: number = 0;

  @Output() pageChange = new EventEmitter<number>();

  get isPrevDisabled(): boolean {
    return this.page === 1;
  }

  get isNextDisabled(): boolean {
    return this.page * this.pageSize >= this.totalCount;
  }

  onPrev(): void {
    if (!this.isPrevDisabled) {
      this.pageChange.emit(this.page - 1);
    }
  }

  onNext(): void {
    if (!this.isNextDisabled) {
      this.pageChange.emit(this.page + 1);
    }
  }
}
