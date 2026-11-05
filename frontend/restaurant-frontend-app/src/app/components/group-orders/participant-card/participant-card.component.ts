import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card', imports: [CurrencyPipe],
  styleUrl: './participant-card.component.scss',
  templateUrl: './participant-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  // що картка отримує ззовні — як параметр функції
  readonly participant = input.required<Participant>();
  readonly isMe = input<boolean>(false);
  readonly canRemove = input<boolean>(false);

  readonly removeClick = output<Participant>();

  // рахується саме, коли змінюється participant
  readonly title = computed(() => {
    if (this.isMe()) {
      return 'My Pre-Order';
    }
    const person = this.participant();
    return person.type === 'Owner' ? `Owner: ${person.name}` : `Guest: ${person.name}`;
  });
}