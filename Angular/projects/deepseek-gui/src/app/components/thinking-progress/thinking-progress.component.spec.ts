import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThinkingProgressComponentComponent } from './thinking-progress.component';

describe('ThinkingProgressComponentComponent', () => {
  let component: ThinkingProgressComponentComponent;
  let fixture: ComponentFixture<ThinkingProgressComponentComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ThinkingProgressComponentComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ThinkingProgressComponentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
