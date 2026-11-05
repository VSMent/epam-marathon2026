import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { GROUP_ORDER_PREVIEW } from '@app/shared/mocks/group-orders/group-order.preview';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { CurrencyPipe, DatePipe } from '@angular/common';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, CurrencyPipe, DatePipe],
  styleUrl: './group-order-page.component.scss',
  templateUrl: './group-order-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent {
  // поки що статичні дані; на рівні 2 їх замінить відповідь API
  readonly participants = signal(GROUP_ORDER_PREVIEW.sessions.participants);
  readonly invite = signal(GROUP_ORDER_PREVIEW.invite);
  readonly total = signal(GROUP_ORDER_PREVIEW.sessions.total);
}