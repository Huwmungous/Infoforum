import { TestBed } from '@angular/core/testing';

import { IfauthLibService } from './ifauth-lib.service';

describe('IfauthLibService', () => {
  let service: IfauthLibService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(IfauthLibService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
