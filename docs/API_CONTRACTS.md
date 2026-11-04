# Group Pre-Order API Contracts (Backend Homework #5 / Frontend #6 / AQA #8)

This document specifies the exact REST API contracts implemented in the backend Lambda (`api-handler`), deployed to AWS API Gateway, and required for **Frontend Homework #6** and **Test Automation (AQA) Homework #8**.

---

## 🌐 Environment & Authentication

* **Live Base URL:**  
  `https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop`
* **Region:**  
  `us-west-2`
* **Authentication Header:**  
  All owner endpoints require a valid Cognito User Pool JWT Bearer token:
  ```http
  Authorization: Bearer <ID_TOKEN_OR_ACCESS_TOKEN>
  ```
* **Content-Type:**  
  `application/json`

---

## 📌 Summary of Endpoints

| Level | Method | Endpoint | Description | Auth Required |
| :--- | :--- | :--- | :--- | :--- |
| **Level 1** | `GET` | `/guest/groups/{groupReservationId}/sessions` | Get participants list, selected dishes, subtotals, and overall total | **Yes** (Owner) |
| **Level 2** | `GET` | `/reservations/{reservationId}/guest-session-id` | Get Owner's own `guestSessionId` for the reservation | **Yes** (Owner) |
| **Level 2** | `POST` | `/group-reservations/{groupReservationId}/submit` | Confirm consolidated order across submitted sessions & submit to restaurant | **Yes** (Owner) |
| **Level 3** | `DELETE` | `/guest/groups/{groupReservationId}/sessions/{guestSessionId}` | Remove a guest session from the group and return updated snapshot | **Yes** (Owner) |

---

## 🟢 Level 1: Get Group Sessions

Retrieves all participant sessions belonging to a group reservation, including each participant's dishes, personal subtotal, submission status, and the grand total across all participants.

### HTTP Request
```http
GET /guest/groups/{groupReservationId}/sessions HTTP/1.1
Host: cs624wcc74.execute-api.us-west-2.amazonaws.com
Authorization: Bearer <TOKEN>
```

#### Path Parameters
* `groupReservationId` (string, UUID, required): The ID of the group reservation.

---

### HTTP Responses

#### ✅ 200 OK
Returns the list of participants and total price.

```json
{
  "participants": [
    {
      "id": "c1f7a188-75c6-4d2b-9e32-a169b1fa5910",
      "type": "Owner",
      "name": "Dmytro",
      "items": [
        {
          "dish": {
            "id": "d-101",
            "name": "Ribeye Steak",
            "price": 450,
            "imageUrl": "https://example.com/images/steak.jpg",
            "weight": "350g"
          },
          "quantity": 1
        },
        {
          "dish": {
            "id": "d-202",
            "name": "Red Wine Cabernet",
            "price": 180,
            "imageUrl": "https://example.com/images/wine.jpg",
            "weight": "150ml"
          },
          "quantity": 1
        }
      ],
      "total": 630,
      "isSubmitted": true
    },
    {
      "id": "b8a95d77-1d54-47cf-89f5-e2f9cd9f7331",
      "type": "Guest",
      "name": "Olena",
      "items": [
        {
          "dish": {
            "id": "d-303",
            "name": "Caesar Salad with Chicken",
            "price": 220,
            "imageUrl": "https://example.com/images/caesar.jpg",
            "weight": "250g"
          },
          "quantity": 2
        }
      ],
      "total": 440,
      "isSubmitted": false
    }
  ],
  "total": 1070
}
```

#### ❌ 400 Bad Request
Missing or invalid `groupReservationId`.
```json
{
  "message": "Group reservation ID is required."
}
```

#### ❌ 401 Unauthorized
Missing or expired Cognito bearer token.
```json
{
  "message": "Unauthorized"
}
```

#### ❌ 404 Not Found
Group reservation does not exist.
```json
{
  "message": "Group reservation not found."
}
```

---

## 🔵 Level 2: Owner Session ID & Consolidated Submit

### 1. Get Owner Guest Session ID
Returns the Owner's own `guestSessionId` for a specific reservation. Used by the frontend to label the owner's card as **"My Pre-Order"**.

#### HTTP Request
```http
GET /reservations/{reservationId}/guest-session-id HTTP/1.1
Host: cs624wcc74.execute-api.us-west-2.amazonaws.com
Authorization: Bearer <TOKEN>
```

#### Path Parameters
* `reservationId` (string, UUID, required): The reservation ID.

#### ✅ 200 OK
```json
{
  "guestSessionId": "c1f7a188-75c6-4d2b-9e32-a169b1fa5910"
}
```

#### ❌ 404 Not Found
```json
{
  "message": "Reservation or owner session not found."
}
```

---

### 2. Submit Consolidated Group Pre-Order
Submits the group pre-order to the restaurant.  
**Business Rules:**
- Only dishes from sessions where `"isSubmitted": true` are included.
- Identical dishes are aggregated by `dishId`, summing their quantities.
- Sets order status to `SubmittedByCustomer`.
- Sets group reservation status to `Submitted`.

#### HTTP Request
```http
POST /group-reservations/{groupReservationId}/submit HTTP/1.1
Host: cs624wcc74.execute-api.us-west-2.amazonaws.com
Authorization: Bearer <TOKEN>
Content-Type: application/json

{
  "reservationId": "f78d65a2-3f11-4770-983e-108215cc79b2"
}
```

#### Request Body Schema
| Field | Type | Required | Description |
| :--- | :--- | :--- | :--- |
| `reservationId` | string | Yes | The ID of the reservation associated with the group |

#### ✅ 200 OK
Returns the list of consolidated dishes with aggregated quantities.
```json
[
  {
    "dishId": "d-101",
    "quantity": 1
  },
  {
    "dishId": "d-202",
    "quantity": 1
  }
]
```

#### ❌ 400 Bad Request
Occurs if:
- No sessions in the group are in `Submitted` status.
- Submitted sessions contain zero dishes.
- The associated `Order` record was not found or is in an invalid state.
```json
{
  "message": "No submitted guest sessions found to consolidate."
}
```

#### ❌ 404 Not Found
Group reservation or associated reservation not found.
```json
{
  "message": "Group reservation not found."
}
```

---

## 🟣 Level 3: Delete Guest Session from Group

Allows the Owner to remove an unsubmitted or unwanted guest session from the group pre-order.  
**Business Rules:**
- Only the Owner can delete a guest session.
- The Owner cannot delete their own session (`type === 'Owner'`).
- Returns the updated group snapshot immediately (recalculated participants list and total).

### HTTP Request
```http
DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId} HTTP/1.1
Host: cs624wcc74.execute-api.us-west-2.amazonaws.com
Authorization: Bearer <TOKEN>
```

#### Path Parameters
* `groupReservationId` (string, UUID, required): The group reservation ID.
* `guestSessionId` (string, UUID, required): The guest session ID to remove.

#### ✅ 200 OK
Returns the updated participants snapshot and new grand total:
```json
{
  "participants": [
    {
      "id": "c1f7a188-75c6-4d2b-9e32-a169b1fa5910",
      "type": "Owner",
      "name": "Dmytro",
      "items": [
        {
          "dish": {
            "id": "d-101",
            "name": "Ribeye Steak",
            "price": 450,
            "weight": "350g"
          },
          "quantity": 1
        }
      ],
      "total": 450,
      "isSubmitted": true
    }
  ],
  "total": 450
}
```

#### ❌ 400 Bad Request
Attempting to delete the Owner's own session or session does not belong to the group.
```json
{
  "message": "Cannot delete the owner's session from the group reservation."
}
```

#### ❌ 404 Not Found
Guest session or group reservation does not exist.
```json
{
  "message": "Guest session not found."
}
```

---

## 🎯 Usage in Next Homeworks

### Frontend Homework #6
* **Levels 1 & 2:** Handled with mock backend (`frontend/restaurant-frontend-app/src/app/shared/mocks/group-orders/`). The mock mirrors the schemas defined above.
* **Level 3 (Live Backend Integration):**  
  The Angular application connects directly to the live AWS API Gateway base URL:  
  `https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop`  
  It polls `GET /guest/groups/{groupReservationId}/sessions` every 15 seconds to update participants in real time.

### Test Automation (AQA) Homework #8
* End-to-end BDD tests (using Reqnroll / C#) call these endpoints directly against the live AWS environment to verify:
  1. Session retrieval and price calculation accuracy.
  2. Owner session identification.
  3. Pre-order consolidation logic and state transition to `SubmittedByCustomer`.
  4. Guest deletion and recalculated response.

