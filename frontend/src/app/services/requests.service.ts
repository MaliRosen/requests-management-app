import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import { RequestDto } from '../models/request.model';
import { SearchQuery } from '../models/search-query.model';

@Injectable({ providedIn: 'root' })
export class RequestsService {
  private readonly baseUrl = environment?.apiUrl ?? 'http://localhost:5000';

  constructor(private http: HttpClient) {}

  // הזהות מגיעה מה-JWT token דרך ה-authInterceptor — לא נשלחת כ-header ידני
  search(query: SearchQuery): Observable<PagedResult<RequestDto>> {
    let params = new HttpParams();

    if (query.requestNumber != null) {
      params = params.set('requestNumber', query.requestNumber);
    }

    if (query.status != null) {
      for (const s of query.status) {
        params = params.append('status', String(s));
      }
    }

    if (query.requestType != null) {
      for (const t of query.requestType) {
        params = params.append('requestType', String(t));
      }
    }

    if (query.createdFrom != null) {
      params = params.set('createdFrom', query.createdFrom);
    }

    if (query.createdTo != null) {
      params = params.set('createdTo', query.createdTo);
    }

    params = params.set('sortBy', query.sortBy);
    params = params.set('sortDirection', query.sortDirection);
    params = params.set('page', String(query.page));
    params = params.set('pageSize', String(query.pageSize));

    return this.http.get<PagedResult<RequestDto>>(`${this.baseUrl}/api/requests/search`, {
      params,
    });
  }
}
