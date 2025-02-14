import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChatGenComponent } from './chat-gen.component';

describe('ChatGenComponent', () => {
  let component: ChatGenComponent;
  let fixture: ComponentFixture<ChatGenComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChatGenComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ChatGenComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
