import boto3
import json
import os
from decimal import Decimal

REGION = "us-west-2"

def parse_decimals(obj):
    """Recursively converts float/int to Decimal for DynamoDB serialization."""
    if isinstance(obj, list):
        return [parse_decimals(i) for i in obj]
    elif isinstance(obj, dict):
        return {k: parse_decimals(v) for k, v in obj.items()}
    elif isinstance(obj, (int, float)) and not isinstance(obj, bool):
        return Decimal(str(obj))
    return obj

def seed():
    data_path = os.path.join(os.path.dirname(__file__), "..", "data", "initial-seed-data.json")
    with open(data_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    dynamodb = boto3.resource("dynamodb", region_name=REGION)
    
    tables_to_seed = [
        "Locations",
        "Tables",
        "Waiters",
        "Dishes",
        "Reservations",
        "GroupReservations",
        "GuestSessions",
        "Orders",
        "Feedback",
        "Notifications"
    ]

    for table_name in tables_to_seed:
        items = data.get(table_name, [])
        if not items:
            continue
        table = dynamodb.Table(table_name)
        print(f"Seeding {len(items)} items into DynamoDB table '{table_name}'...")
        for item in items:
            item_decimal = parse_decimals(item)
            table.put_item(Item=item_decimal)

    print("\n[SUCCESS] Successfully seeded all tables in AWS DynamoDB (us-west-2) from data/initial-seed-data.json!")

if __name__ == "__main__":
    seed()
