import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DeepseekComponent } from './deepseek.component';

describe('DeepseekComponent', () => {
  let component: DeepseekComponent;
  let fixture: ComponentFixture<DeepseekComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DeepseekComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DeepseekComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
