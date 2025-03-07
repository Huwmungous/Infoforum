import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CodeGenResponseComponent } from './code-gen-response.component';

describe('CodeGenResponseComponent', () => {
  let component: CodeGenResponseComponent;
  let fixture: ComponentFixture<CodeGenResponseComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CodeGenResponseComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CodeGenResponseComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
