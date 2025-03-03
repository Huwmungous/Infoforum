import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IfauthLibComponent } from './ifauth-lib.component';

describe('IfauthLibComponent', () => {
  let component: IfauthLibComponent;
  let fixture: ComponentFixture<IfauthLibComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IfauthLibComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(IfauthLibComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
