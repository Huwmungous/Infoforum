import { TestBed } from '@angular/core/testing';

import { IFSharedLibraryService } from './ifshared-library.service';

describe('IFSharedLibraryService', () => {
  let service: IFSharedLibraryService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(IFSharedLibraryService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
