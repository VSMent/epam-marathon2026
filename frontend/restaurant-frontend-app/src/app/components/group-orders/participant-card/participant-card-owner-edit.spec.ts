import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Participant } from '@app/models/group-orders/participant.model';

import { ParticipantCardComponent } from './participant-card.component';

describe('ParticipantCardComponent · owner Edit my Pre-Order', () => {
  let fixture: ComponentFixture<ParticipantCardComponent>;
  let component: ParticipantCardComponent;

  const owner: Participant = {
    id: 'owner-1',
    type: 'Owner',
    name: 'Yevhenii',
    items: [],
    total: 0,
    isSubmitted: false,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ParticipantCardComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('participant', owner);
  });

  function findEditButton(): HTMLButtonElement | null {
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('.edit-btn');
  }

  it('shows the Edit button only when canEditOwn is true', () => {
    fixture.componentRef.setInput('canEditOwn', false);
    expect(findEditButton()).toBeNull();

    fixture.componentRef.setInput('canEditOwn', true);
    expect(findEditButton()).not.toBeNull();
  });

  it('emits editClick with the owner when the Edit button is clicked', () => {
    fixture.componentRef.setInput('canEditOwn', true);
    spyOn(component.editClick, 'emit');

    findEditButton()!.click();

    expect(component.editClick.emit).toHaveBeenCalledWith(owner);
  });
});
