import {
  Component,
  Input,
  Output,
  EventEmitter,
  AfterViewChecked,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { mapDeepseekToHighlight } from '../../../deepseek/deepseek-to-highlight-map';

declare const hljs: any;

interface ChatChunk {
  response: string;
  done: boolean;
  // Add other properties if needed
}

@Component({
  selector: 'app-code-gen-response',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule, MatExpansionModule],
  templateUrl: './code-gen-response.component.html',
  styleUrls: ['./code-gen-response.component.scss'],
})
export class CodeGenResponseComponent implements AfterViewChecked {
  @Output() delete = new EventEmitter<void>();

  sections: {
    type: string;
    content: string;
    language?: string;
    showThinking: boolean;
  }[] = [];

  @Input() prompt: string = '';

  private _response: string = '';

  @Input()
  set response(value: string) {
    if (value !== this._response) {
      this._response = value;
      this.resetBuffers();
      this.processChunk({ response: value, done: true });
    }
  }
  get response(): string {
    return this._response;
  }

  private insideThink = false;
  private normalTextBuffer = '';
  private thinkTextBuffer = '';
  private partialChunk = '';

  private needsHighlight = false;

  private resetBuffers() {
    this.insideThink = false;
    this.normalTextBuffer = '';
    this.thinkTextBuffer = '';
    this.partialChunk = '';
    this.sections = [];
  }

  processChunk(chunk: ChatChunk) {
    this.partialChunk += chunk.response;

    while (this.partialChunk.length > 0) {
      if (this.insideThink) {
        const endIdx = this.partialChunk.indexOf('</think>');
        if (endIdx === -1) {
          this.thinkTextBuffer += this.partialChunk;
          this.partialChunk = '';
          break;
        } else {
          this.thinkTextBuffer += this.partialChunk.slice(0, endIdx);
          this.partialChunk = this.partialChunk.slice(endIdx + 8);
          this.insideThink = false;
          this.updateSections();
        }
      } else {
        const startIdx = this.partialChunk.indexOf('<think>');
        if (startIdx === -1) {
          this.normalTextBuffer += this.partialChunk;
          this.partialChunk = '';
          // Defer updating sections for normal text to parseCodeBlocks
          break;
        } else if (startIdx > 0) {
          this.normalTextBuffer += this.partialChunk.slice(0, startIdx);
          this.partialChunk = this.partialChunk.slice(startIdx);
          // Will loop again to handle <think>
        } else {
          this.partialChunk = this.partialChunk.slice(7);
          this.insideThink = true;
          this.updateSections();
        }
      }
    }

    // Now parse and update text + code sections from normalTextBuffer only
    this.parseCodeBlocks();

    this.needsHighlight = true;
  }

  private updateSections() {
    // Only manage think sections here, avoid duplicating text/code sections
    this.sections = this.sections.filter(s => s.type !== 'think');

    if (this.thinkTextBuffer.trim()) {
      this.sections.push({
        type: 'think',
        content: this.thinkTextBuffer.trim(),
        showThinking: false,
      });
    }
  }

  private formatMarkdown(text: string): string {
    return text
      .trim()
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/###(.*?)(\n|$)/g, '<em>$1</em>$2');
  }

  private parseCodeBlocks() {
    const codeBlockRegex = /```([\w]*)\n([\s\S]*?)```/g;
    const newSections: typeof this.sections = [];

    let lastIndex = 0;
    let match;

    while ((match = codeBlockRegex.exec(this.normalTextBuffer)) !== null) {
      const [fullMatch, lang, code] = match;
      const index = match.index;

      if (index > lastIndex) {
        const precedingText = this.normalTextBuffer.slice(lastIndex, index);
        if (precedingText.trim()) {
          newSections.push({
            type: 'text',
            content: this.formatMarkdown(precedingText),
            showThinking: false,
          });
        }
      }

      newSections.push({
        type: 'code',
        content: code.trim(),
        language: mapDeepseekToHighlight(lang.trim()),
        showThinking: false,
      });

      lastIndex = index + fullMatch.length;
    }

    if (lastIndex < this.normalTextBuffer.length) {
      const remainingText = this.normalTextBuffer.slice(lastIndex);
      if (remainingText.trim()) {
        newSections.push({
          type: 'text',
          content: this.formatMarkdown(remainingText),
          showThinking: false,
        });
      }
    }

    // Remove old text and code sections before adding new parsed ones
    this.sections = this.sections.filter(s => s.type !== 'text' && s.type !== 'code');

    this.sections.push(...newSections);
  }

  ngAfterViewChecked() {
    if (this.needsHighlight) {
      this.highlightCode();
      this.needsHighlight = false;
    }
  }

  highlightCode() {
    document.querySelectorAll('pre code').forEach((block) => {
      hljs.highlightBlock(block as HTMLElement);
    });
  }

  copyToClipboard(content: string) {
    navigator.clipboard.writeText(content).catch((err) => {
      console.error('Could not copy text: ', err);
    });
  }

  deleteResponse() {
    this.delete.emit();
  }
}
