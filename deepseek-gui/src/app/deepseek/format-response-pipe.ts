// format-response.pipe.ts
import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Pipe({
  name: 'formatResponse',
  standalone: true // Make the pipe standalone so it can be imported directly.
})
export class FormatResponsePipe implements PipeTransform {
  constructor(private sanitizer: DomSanitizer) {}

  transform(value: string): SafeHtml {
    if (!value) {
      return '';
    }

    // Replace code blocks delimited by triple backticks with <pre class="code-block"> ... </pre>
    let formatted = value.replace(/```([\s\S]*?)```/g, (match, codeContent) => {
      return `<pre class="code-block">${codeContent}</pre>`;
    });

    // Wrap the entire response in a container with the default text styling.
    formatted = `<div class="text-block">${formatted}</div>`;

    // Bypass Angular's security for the formatted HTML.
    return this.sanitizer.bypassSecurityTrustHtml(formatted);
  }
}
