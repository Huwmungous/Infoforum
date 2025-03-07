import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CodeGenComponent } from './code-gen.component';

describe('CodeGenComponent', () => {
  let component: CodeGenComponent;
  let fixture: ComponentFixture<CodeGenComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CodeGenComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CodeGenComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
