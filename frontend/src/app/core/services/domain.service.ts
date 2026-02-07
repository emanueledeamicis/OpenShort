import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Domain, CreateDomainDto } from '../models/api.models';

@Injectable({
    providedIn: 'root'
})
export class DomainService {
    private apiUrl = '/api/domains';

    constructor(private http: HttpClient) { }

    getAll(): Observable<Domain[]> {
        return this.http.get<Domain[]>(this.apiUrl);
    }

    getById(id: number): Observable<Domain> {
        return this.http.get<Domain>(`${this.apiUrl}/${id}`);
    }

    create(dto: CreateDomainDto): Observable<Domain> {
        return this.http.post<Domain>(this.apiUrl, dto);
    }

    update(id: number, dto: Partial<Domain>): Observable<Domain> {
        return this.http.put<Domain>(`${this.apiUrl}/${id}`, dto);
    }

    delete(id: number): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}`);
    }

    getLinkCount(id: number): Observable<number> {
        return this.http.get<number>(`${this.apiUrl}/${id}/link-count`);
    }
}
