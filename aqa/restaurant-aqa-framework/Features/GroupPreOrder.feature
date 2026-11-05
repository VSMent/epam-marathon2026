Feature: Group pre-order

  The organiser of a group booking pre-orders for the whole group on the group
  pre-order screen: who has ordered what, which drinks need a ride home, and who
  still owes a dish. Every scenario runs on the standing fixture restaurant and
  creates its own booking and its own guests.

  Background:
    Given the owner has a group pre-order at the fixture restaurant
    And the owner has signed in

  Rule: Identical items from several guests are offered as a bulk option

    # Known defect (expected to fail until fixed): the backend has no bulk equivalents for dishes,
    # so the screen never offers a combined option such as a 1 L teapot instead of three cups.
    @ui @known-defect
    Scenario: Three guests who order the same tea are offered a bulk option
      Given guest "Olena" has ordered "Jasmine Tea Cup"
      And guest "Marta" has ordered "Jasmine Tea Cup"
      And guest "Taras" has ordered "Jasmine Tea Cup"
      When the owner opens the group pre-order
      Then a bulk option for "Jasmine Tea Cup" is offered

    @ui
    Scenario: Guests who order different dishes are not offered a bulk option
      Given guest "Olena" has ordered "Jasmine Tea Cup"
      And guest "Marta" has ordered "Caesar Salad"
      When the owner opens the group pre-order
      Then no bulk option is offered

  Rule: Alcoholic drinks are marked and can be filtered for transport planning

    @ui
    Scenario: Guests with an alcoholic drink are marked on the screen
      Given guest "Olena" has ordered the alcoholic drink
      And guest "Marta" has ordered "Caesar Salad"
      When the owner opens the group pre-order
      Then guest "Olena" is marked as having alcohol
      And guest "Marta" is not marked as having alcohol

    @ui
    Scenario: Show Alcohol Only leaves just the alcoholic drinks, grouped by guest
      Given guest "Olena" has ordered the alcoholic drink
      And guest "Marta" has ordered "Caesar Salad"
      When the owner opens the group pre-order
      And the owner turns on the alcohol filter
      Then only alcoholic drinks are listed under their guests
      And guest "Marta" has no alcoholic drinks to show

    @ui
    Scenario: Plan Logistics offers ride-sharing partners with the restaurant address as destination
      Given guest "Olena" has ordered the alcoholic drink
      When the owner opens the group pre-order
      And the owner plans transport
      Then ride-sharing partners are offered for the restaurant address

  Rule: Each guest's dishes and subtotal are listed, and guests who have not ordered are flagged

    @ui
    Scenario: Each guest's dish and subtotal are listed under their name
      Given guest "Olena" has ordered "Caesar Salad"
      And guest "Marta" has ordered "Jasmine Tea Cup"
      When the owner opens the group pre-order
      Then guest "Olena" is listed with the dish "Caesar Salad"
      And the subtotal of guest "Olena" is the price of "Caesar Salad"

    @ui
    Scenario: A guest with no dishes is flagged and can be reminded
      Given guest "Olena" has joined the group
      And guest "Marta" has ordered "Caesar Salad"
      When the owner opens the group pre-order
      Then guest "Olena" is flagged as having no dishes yet
      And guest "Olena" can be sent a reminder
      And guest "Marta" is not flagged as having no dishes yet

    # Known defect (expected to fail until fixed): the backend has no route for reminders, so the
    # reminder call is refused. The screen still shows the confirmation, which is why the status is checked.
    @ui @known-defect
    Scenario: A sent reminder reaches the guest, is confirmed and is marked as sent
      Given guest "Olena" has joined the group
      When the owner opens the group pre-order
      And the owner sends a reminder to "Olena"
      Then the reminder to "Olena" is delivered
      And a confirmation toast says "Reminder notification sent to Olena"
      And the reminder to "Olena" is marked as sent
