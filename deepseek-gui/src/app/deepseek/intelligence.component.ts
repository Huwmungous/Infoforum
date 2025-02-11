import { Component } from '@angular/core';
import { CommonModule } from '@angular/common'; 
import { MatTabsModule } from '@angular/material/tabs';
import { CodeGenComponent } from '../components/code-gen/code-gen.component';

@Component({
  selector: 'app-intelligence',
  templateUrl: './intelligence.component.html',
  styleUrls: ['./intelligence.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    MatTabsModule,
    CodeGenComponent
  ]
})
export class IntelligenceComponent { 

}