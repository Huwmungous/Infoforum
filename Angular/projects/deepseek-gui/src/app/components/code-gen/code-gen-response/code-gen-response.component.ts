import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { mapDeepseekToHighlight } from '../../../deepseek/deepseek-to-highlight-map';


declare const hljs: any;

@Component({
  selector: 'app-code-gen-response',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatExpansionModule
  ],
  templateUrl: './code-gen-response.component.html',
  styleUrls: ['./code-gen-response.component.scss']
})
export class CodeGenResponseComponent {
  @Output() delete = new EventEmitter<void>();
  
  sections: { type: string, content: string, language?: string, showThinking: boolean }[] = [];

  prompt: string = '';
  private partialChunk: string = '';

  ngAfterViewInit(): void {
    hljs.configure({ ignoreUnescapedHTML: true });
    this.highlightCode();
  }

  processChunk(chunk: string) { 
    this.partialChunk += chunk; 
    const parts = this.partialChunk.split(/(```[\s\S]*?```|<think>[\s\S]*?<\/think>)/g); 
    // Process each part
    this.sections = parts.map((part, index) => {
      const partIsCode = part.startsWith('```');
      const partIsThink = part.startsWith('<think>');
      const isComplete = partIsCode && part.endsWith('```') || partIsThink && part.endsWith('</think>');
      if (partIsCode && isComplete) {
        const content = part.slice(3, -3);
        const firstLineEnd = content.indexOf('\n');
        const language = content.slice(0, firstLineEnd).trim();
        const code = content.slice(firstLineEnd + 1).trim();
        return { type: 'code', content: code, language: mapDeepseekToHighlight(language), showThinking: false };
      } else if (partIsThink && isComplete) {
        const content = part.slice(7, -8).trim();
        return { type: 'think', content: content, showThinking: false  };
      } else if ((partIsCode || partIsThink) && !isComplete) {
        this.partialChunk = part; 
        return null;
      } else {
        const formattedContent = part.trim()
        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>') 
        .replace(/###(.*?)(\n|$)/g, '<em>$1</em>$2'); 
        return { type: 'text', content: formattedContent, showThinking: false  };
      }
    }).filter(section => section !== null);

    this.highlightCode();
  }

  highlightCode() {
    document.querySelectorAll('pre code').forEach((block) => {
      hljs.highlightBlock(block as HTMLElement);
    });
  }

  copyToClipboard(content: string) {
    navigator.clipboard.writeText(content).then(() => { 
    }).catch(err => {
      console.error('Could not copy text: ', err);
    });
  }

  saveResponse() {
    console.log('Save response');
    // Implement your save logic here
  }

  deleteResponse() {
    this.delete.emit();
  }
}