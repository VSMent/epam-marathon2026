import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card',
  imports: [CurrencyPipe],
  styleUrl: './participant-card.component.scss',
  templateUrl: './participant-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  readonly participant = input.required<Participant>();
  readonly isMe = input<boolean>(false);
  readonly canRemove = input<boolean>(false);
  readonly showAlcoholOnly = input<boolean>(false);

  readonly removeClick = output<Participant>();
  readonly reminderClick = output<Participant>();

  readonly title = computed(() => {
    if (this.isMe()) {
      return 'My Pre-Order';
    }
    const person = this.participant();
    return person.type === 'Owner' ? `Owner: ${person.name}` : `Guest: ${person.name}`;
  });

  readonly hasAlcohol = computed(() =>
    this.participant().items.some((i) => i.dish.isAlcohol)
  );

  readonly hasEmptyCart = computed(() => this.participant().items.length === 0);

  readonly displayedItems = computed(() => {
    const items = this.participant().items;
    if (!this.showAlcoholOnly()) {
      return items;
    }
    return items.filter((i) => i.dish.isAlcohol);
  });

  readonly subtotal = computed(() => {
    if (!this.showAlcoholOnly()) {
      return this.participant().total;
    }
    return this.displayedItems().reduce(
      (sum, item) => sum + item.dish.price * item.quantity,
      0
    );
  });
}
