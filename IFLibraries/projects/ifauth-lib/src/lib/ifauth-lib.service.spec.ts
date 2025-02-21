import { TestBed } from '@angular/core/testing';

import { IFAuthLibService } from './ifauth-lib.service';

describe('IFAuthLibService', () => {
  let service: IFAuthLibService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(IFAuthLibService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
