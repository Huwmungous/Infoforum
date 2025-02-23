import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IFAuthLibComponent } from './ifauth-lib.component';

describe('IFAuthLibComponent', () => {
  let component: IFAuthLibComponent;
  let fixture: ComponentFixture<IFAuthLibComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IFAuthLibComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(IFAuthLibComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
