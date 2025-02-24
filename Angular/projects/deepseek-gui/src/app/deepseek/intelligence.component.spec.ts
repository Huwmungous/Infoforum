import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IntelligenceComponent } from './intelligence.component';

describe('DeepseekComponent', () => {
  let component: IntelligenceComponent;
  let fixture: ComponentFixture<IntelligenceComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IntelligenceComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(IntelligenceComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
