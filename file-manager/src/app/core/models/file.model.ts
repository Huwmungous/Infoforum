
// src/app/core/models/file.model.ts
export interface FileNode {
    id: string;
    name: string;
    path: string;
    type: 'file';
    size: number;
    lastModified: Date;
    mimeType?: string;
  }
  
  // src/app/core/models/folder.model.ts
  export interface FolderNode {
    id: string;
    name: string;
    path: string;
    type: 'folder';
    lastModified: Date;
    children?: (FileNode | FolderNode)[];
    isExpanded?: boolean;
    isLoading?: boolean;
  }
  
  export type FileSystemNode = FileNode | FolderNode;
  
  export function isFolder(node: FileSystemNode): node is FolderNode {
    return node.type === 'folder';
  }
  
  export function isFile(node: FileSystemNode): node is FileNode {
    return node.type === 'file';
  }