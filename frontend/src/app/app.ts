import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, of } from 'rxjs';
import { switchMap, finalize, catchError } from 'rxjs/operators';

import { AuthService, CurrentUser } from './services/auth.service';
import { RequestsService } from './services/requests.service';
import { SearchQuery } from './models/search-query.model';
import { PagedResult } from './models/paged-result.model';
import { RequestDto } from './models/request.model';
import { SearchFilterComponent } from './components/search-filter/search-filter.component';
import { RequestsTableComponent } from './components/requests-table/requests-table.component';
import { PaginationComponent } from './components/pagination/pagination.component';
import { LoginComponent } from './components/login/login.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    LoginComponent,
    SearchFilterComponent,
    RequestsTableComponent,
    PaginationComponent,
  ],
  templateUrl: './app.html',
  styleUrls: ['./app.scss'],
})
export class App implements OnInit {
  isLoggedIn = false;
  currentUser: CurrentUser | null = null;

  query: SearchQuery = {
    sortBy: 'CreatedAt',
    sortDirection: 'desc',
    page: 1,
    pageSize: 20,
  };

  result: PagedResult<RequestDto> | null = null;
  loading = false;
  error: string | null = null;
  dateRangeError: string | null = null;

  private searchTrigger$ = new Subject<SearchQuery>();

  constructor(
    private authService: AuthService,
    private requestsService: RequestsService,
  ) {}

  ngOnInit(): void {
    this.isLoggedIn = this.authService.isLoggedIn();
    this.currentUser = this.authService.getCurrentUser();

    this.searchTrigger$.pipe(
      switchMap(query => {
        this.loading = true;
        this.error = null;
        return this.requestsService.search(query).pipe(
          finalize(() => this.loading = false),
          catchError(err => {
            if (err?.status === 401) {
              this.authService.logout();
              this.isLoggedIn = false;
            } else {
              const serverMessage = err?.error;
              this.error = typeof serverMessage === 'string' && serverMessage.length > 0
                ? serverMessage
                : `שגיאה ${err?.status ?? ''}: אירעה שגיאה בטעינת הבקשות`;
            }
            return of(null);
          })
        );
      })
    ).subscribe(result => {
      if (result !== null) {
        this.result = result;
        this.error = null;
      }
    });

    if (this.isLoggedIn) {
      this.loadRequests();
    }
  }

  onLoggedIn(): void {
    this.isLoggedIn = true;
    this.currentUser = this.authService.getCurrentUser();
    this.loadRequests();
  }

  onLogout(): void {
    this.authService.logout();
    this.isLoggedIn = false;
    this.currentUser = null;
    this.result = null;
    this.error = null;
    this.dateRangeError = null;
  }

  onFilterChange(changes: Partial<SearchQuery>): void {
    this.dateRangeError = null;
    this.error = null;
    this.query = {
      sortBy: this.query.sortBy,
      sortDirection: this.query.sortDirection,
      page: 1,
      pageSize: this.query.pageSize,
      createdFrom: undefined,
      createdTo: undefined,
      ...changes,
    };
    this.loadRequests();
  }

  onSortChange(sort: { sortBy: string; sortDirection: string }): void {
    this.query = { ...this.query, ...sort };
    this.loadRequests();
  }

  onPageChange(page: number): void {
    this.query = { ...this.query, page };
    this.loadRequests();
  }

  loadRequests(): void {
    this.searchTrigger$.next(this.query);
  }
}
