// src/app/file-manager/services/file.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpEvent, HttpEventType, HttpRequest } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators'; 
import { FileSystemNode, FolderNode } from '../core/models/file.model';

@Injectable({
  providedIn: 'root',
})
export class FileService {
  private apiUrl = 'https://your-api-url.com/api/files'; // Adjust to your C# Web API URL

  constructor(private http: HttpClient) {}

  getRootNodes(): Observable<FileSystemNode[]> {
    return this.http.get<FileSystemNode[]>(`${this.apiUrl}/root`)
      .pipe(
        catchError(error => {
          console.error('Error fetching root nodes', error);
          return throwError(() => error);
        })
      );
  }

  getChildNodes(path: string): Observable<FileSystemNode[]> {
    return this.http.get<FileSystemNode[]>(`${this.apiUrl}/children`, {
      params: { path }
    }).pipe(
      catchError(error => {
        console.error(`Error fetching children for path ${path}`, error);
        return throwError(() => error);
      })
    );
  }

  uploadFile(file: File, destinationPath: string): Observable<HttpEvent<any>> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('path', destinationPath);

    const req = new HttpRequest('POST', `${this.apiUrl}/upload`, formData, {
      reportProgress: true
    });

    return this.http.request(req).pipe(
      catchError(error => {
        console.error('Error uploading file', error);
        return throwError(() => error);
      })
    );
  }

  downloadFile(path: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/download`, {
      params: { path },
      responseType: 'blob'
    }).pipe(
      catchError(error => {
        console.error(`Error downloading file at path ${path}`, error);
        return throwError(() => error);
      })
    );
  }

  deleteNode(path: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/delete`, {
      params: { path }
    }).pipe(
      catchError(error => {
        console.error(`Error deleting node at path ${path}`, error);
        return throwError(() => error);
      })
    );
  }

  createFolder(path: string, folderName: string): Observable<FolderNode> {
    return this.http.post<FolderNode>(`${this.apiUrl}/folder`, {
      path,
      folderName
    }).pipe(
      catchError(error => {
        console.error(`Error creating folder ${folderName} at path ${path}`, error);
        return throwError(() => error);
      })
    );
  }
}