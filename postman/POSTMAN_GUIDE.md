# Postman Testing Guide — Group Pre-Order API

This folder contains step-by-step instructions to run and test all group pre-order endpoints using **Postman** (or `curl`) against the **live AWS backend**.

---

## 🌐 Live API Base URL

```text
https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop
```

---

## 🔑 1. Where and How to Get an Auth Token

All owner-level endpoints are protected by an **AWS Cognito User Pool Authorizer**. To make requests, you need an **`idToken`** passed in the `Authorization` header.

### Option A: Use the Pre-Registered Demo Account (Instant)
A test account is already provisioned on the live backend:
* **Email:** `demo_owner@test.com`
* **Password:** `Password123!`

### Option B: Register Your Own Account
If you want to register a new user:
* **Method:** `POST`
* **URL:** `https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/register`
* **Headers:** `Content-Type: application/json`
* **Body (raw JSON):**
  ```json
  {
    "firstName": "Dmytro",
    "lastName": "Owner",
    "email": "my_owner@test.com",
    "password": "Password123!",
    "confirmPassword": "Password123!"
  }
  ```
* **Expected Response (200 OK):**
  ```json
  {
    "message": "User registered and confirmed successfully"
  }
  ```

---

### Step 1.2: Sign In to Get the Token

In Postman, send the following request:

* **Method:** `POST`
* **URL:** `https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/signin`
* **Headers:**
  * `Content-Type: application/json`
* **Body (raw JSON):**
  ```json
  {
    "email": "demo_owner@test.com",
    "password": "Password123!"
  }
  ```

#### Live Response Example (200 OK):
```json
{
  "message": "Sign in successful",
  "idToken": "eyJraWQiOiJFWmRjc3MyeWZQY1d...<LONG_JWT_STRING>",
  "accessToken": "eyJraWQiOiJFWmRjc3MyeWZQY1d...<LONG_JWT_STRING>"
}
```

> [!IMPORTANT]
> **Which token to use in Postman?**  
> Copy the value of **`idToken`**.  
> In Postman, go to the **Authorization** tab of any request, select **Bearer Token**, and paste the `idToken`.  
> Or add a header manually:  
> `Authorization: Bearer <PASTE_ID_TOKEN_HERE>`

---

## 🚀 2. Endpoints & Postman Requests

---

### Request 1: Get Group Sessions (Level 1)
Returns all participant sessions in a group pre-order, including dishes, personal subtotals, and overall group total.

* **Method:** `GET`
* **URL:**  
  `https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/guest/groups/demo-group-1/sessions`
* **Headers:**
  * `Authorization`: `Bearer <YOUR_ID_TOKEN>`

#### Example Response (200 OK):
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
    },
    {
      "id": "b8a95d77-1d54-47cf-89f5-e2f9cd9f7331",
      "type": "Guest",
      "name": "Olena",
      "items": [
        {
          "dish": {
            "id": "d-303",
            "name": "Caesar Salad",
            "price": 220,
            "weight": "250g"
          },
          "quantity": 2
        }
      ],
      "total": 440,
      "isSubmitted": false
    }
  ],
  "total": 890
}
```

#### Error Response (404 Not Found if ID does not exist):
```json
{
  "error": "Group reservation was not found"
}
```

#### Error Response (401 Unauthorized if token missing/expired):
```json
{
  "message": "Unauthorized"
}
```

---

### Request 2: Get Owner Guest Session ID (Level 2)
Used by the frontend to identify which participant card in the list is the Owner's own cart and label it as **"My Pre-Order"**.

* **Method:** `GET`
* **URL:**  
  `https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/reservations/demo-reservation-1/guest-session-id`
* **Headers:**
  * `Authorization`: `Bearer <YOUR_ID_TOKEN>`

#### Example Response (200 OK):
```json
{
  "guestSessionId": "session-owner-1"
}
```

---

### Request 3: Submit Consolidated Pre-Order (Level 2)
Aggregates dishes from all `Submitted` guest sessions into a single order, sums quantities by `dishId`, sets order status to `SubmittedByCustomer`, and transitions group to `Submitted`.

* **Method:** `POST`
* **URL:**  
  `https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/group-reservations/demo-group-1/submit`
* **Headers:**
  * `Authorization`: `Bearer <YOUR_ID_TOKEN>`
  * `Content-Type`: `application/json`
* **Body (raw JSON):**
  ```json
  {
    "reservationId": "demo-reservation-1"
  }
  ```

---

### Request 4: Delete Guest Session from Group (Level 3)
Allows the Owner to remove an unsubmitted guest session from the group. Immediately returns the updated participants list and recalculated total.

* **Method:** `DELETE`
* **URL:**  
  `https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/guest/groups/demo-group-1/sessions/session-guest-4`
* **Headers:**
  * `Authorization`: `Bearer <YOUR_ID_TOKEN>`

#### Example Response (200 OK):
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

#### Error Response (400 Bad Request):
*(Owner cannot delete their own session)*
```json
{
  "error": "Cannot delete owner session from group reservation"
}
```

#### Error Response (404 Not Found):
```json
{
  "error": "Guest session was not found"
}
```

---

## ⚡ Quick Test Script (PowerShell 1-Liner)

If you prefer testing directly in the terminal before opening Postman, run this PowerShell script:

```powershell
# 1. Sign in and capture token
$authBody = '{"email":"demo_owner@test.com","password":"Password123!"}'
$signIn = Invoke-RestMethod -Uri "https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/signin" -Method Post -Body $authBody -ContentType "application/json"
$token = $signIn.idToken
Write-Host "Got Token: $($token.Substring(0, 30))..." -ForegroundColor Green

# 2. Call Get Group Sessions (with real seeded ID: demo-group-1)
$headers = @{ "Authorization" = "Bearer $token" }
try {
    $res = Invoke-RestMethod -Uri "https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/guest/groups/demo-group-1/sessions" -Headers $headers
    $res | ConvertTo-Json -Depth 5
} catch {
    Write-Host "Response code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Yellow
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host "Response body: $($reader.ReadToEnd())" -ForegroundColor Cyan
}
```

