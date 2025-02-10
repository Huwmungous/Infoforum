// format-response.pipe.ts
import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Pipe({
  name: 'formatResponse',
  standalone: true
})
export class FormatResponsePipe implements PipeTransform {
  constructor(private sanitizer: DomSanitizer) {}

  transform(value: string): SafeHtml {
    if (!value) {
      return '';
    }

    // Replace code blocks delimited by triple backticks with <pre class="code-block"> ... </pre>
    const formatted = value.replace(/```([\s\S]*?)```/g, (match, codeContent) => {
      return `<pre class="code-block">${codeContent}</pre>`;
    });

    // Wrap the rest of the text in a div with a custom font class
    const wrapped = `<div class="text-block">${formatted}</div>`;
    return this.sanitizer.bypassSecurityTrustHtml(wrapped);
  }
}
