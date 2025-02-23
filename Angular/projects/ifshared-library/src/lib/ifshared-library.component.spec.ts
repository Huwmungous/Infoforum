import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IFSharedLibraryComponent } from './ifshared-library.component';

describe('IFSharedLibraryComponent', () => {
  let component: IFSharedLibraryComponent;
  let fixture: ComponentFixture<IFSharedLibraryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IFSharedLibraryComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(IFSharedLibraryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
