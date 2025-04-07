// src/app/file-manager/components/file-tree/file-tree.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTreeModule, MatTreeNestedDataSource } from '@angular/material/tree';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NestedTreeControl } from '@angular/cdk/tree'; 
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, finalize, tap } from 'rxjs/operators'; 
import { DragDropDirective } from '../../directives/drag-drop.directove';
import { FileService } from '../../../services/file.service';
import { FileSystemNode, FolderNode, isFolder } from '../../../core/models/file.model';
import { HttpEventType } from '@angular/common/http';

@Component({
  selector: 'app-file-tree',
  standalone: true,
  imports: [
    CommonModule,
    MatTreeModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    DragDropDirective
  ],
  templateUrl: './file-tree.component.html',
  styleUrls: ['./file-tree.component.scss']
})
export class FileTreeComponent implements OnInit {
  treeControl = new NestedTreeControl<FileSystemNode>(node => 
    isFolder(node) ? node.children : []
  );
  
  dataSource = new MatTreeNestedDataSource<FileSystemNode>();
  loading = new BehaviorSubject<boolean>(false);
  loading$ = this.loading.asObservable();
  
  constructor(private fileService: FileService) {}
  
  ngOnInit(): void {
    this.loadRootNodes();
  }
  
  loadRootNodes(): void {
    this.loading.next(true);
    this.fileService.getRootNodes().pipe(
      tap(nodes => {
        this.dataSource.data = nodes;
      }),
      catchError(error => {
        console.error('Error loading root nodes', error);
        return of([]);
      }),
      finalize(() => this.loading.next(false))
    ).subscribe();
  }
  
  hasChild = (_: number, node: FileSystemNode) => isFolder(node);
  
  loadChildren(node: FolderNode): void {
    if (!node.children || node.children.length === 0) {
      node.isLoading = true;
      this.fileService.getChildNodes(node.path).pipe(
        tap(children => {
          node.children = children;
          node.isExpanded = true;
          // Force refresh the tree
          this.dataSource.data = [...this.dataSource.data];
        }),
        catchError(error => {
          console.error(`Error loading children for ${node.path}`, error);
          return of([]);
        }),
        finalize(() => {
          node.isLoading = false;
          // Force refresh the tree again to reflect loading state change
          this.dataSource.data = [...this.dataSource.data];
        })
      ).subscribe();
    }
  }
  
  toggleNode(node: FolderNode): void {
    if (node.isExpanded) {
      // Collapse node and clear children to save memory
      node.isExpanded = false;
      node.children = [];
      this.treeControl.collapse(node);
      // Force refresh the tree
      this.dataSource.data = [...this.dataSource.data];
    } else {
      this.loadChildren(node);
    }
  }
  
  handleFileClick(node: FileSystemNode): void {
    if (isFolder(node)) {
      this.toggleNode(node);
    } else {
      this.downloadFile(node);
    }
  }
  
  downloadFile(node: FileSystemNode): void {
    this.fileService.downloadFile(node.path).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = node.name;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    });
  }
  
  onFileDrop(event: any, node: FolderNode): void {
    event.preventDefault();
    const files: FileList = event.dataTransfer.files;
    
    if (files.length > 0) {
      for (let i = 0; i < files.length; i++) {
        this.uploadFile(files[i], node.path);
      }
    }
  }
  
  uploadFile(file: File, path: string): void {
    this.fileService.uploadFile(file, path).subscribe(event => {
      if (event.type === HttpEventType.Response) {
        // Refresh the folder contents after successful upload
        const folderNode = this.findFolderByPath(path);
        if (folderNode && folderNode.isExpanded) {
          this.loadChildren(folderNode);
        }
      }
    });
  }
  
  findFolderByPath(path: string): FolderNode | null {
    const findNode = (nodes: FileSystemNode[], targetPath: string): FolderNode | null => {
      for (const node of nodes) {
        if (isFolder(node)) {
          if (node.path === targetPath) {
            return node;
          }
          
          if (node.children && node.children.length > 0) {
            const foundNode = findNode(node.children, targetPath);
            if (foundNode) {
              return foundNode;
            }
          }
        }
      }
      return null;
    };
    
    return findNode(this.dataSource.data, path);
  }
}