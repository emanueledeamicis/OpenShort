import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Link, CreateLinkDto, UpdateLinkDto } from '../models/api.models';

@Injectable({
    providedIn: 'root'
})
export class LinkService {
    private apiUrl = '/api/links';

    constructor(private http: HttpClient) { }

    getAll(): Observable<Link[]> {
        return this.http.get<Link[]>(this.apiUrl);
    }

    getById(id: number): Observable<Link> {
        return this.http.get<Link>(`${this.apiUrl}/${id}`);
    }

    create(dto: CreateLinkDto): Observable<Link> {
        return this.http.post<Link>(this.apiUrl, dto);
    }

    update(id: number, dto: UpdateLinkDto): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/${id}`, dto);
    }

    delete(id: number): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}`);
    }
}
