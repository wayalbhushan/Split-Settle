import os
import sys
import json
from decimal import Decimal

# Add reference/splitledger-django to path
repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ref_django = os.path.join(repo_root, 'reference', 'splitledger-django')
if ref_django not in sys.path:
    sys.path.insert(0, ref_django)

import django
from django.conf import settings

if not settings.configured:
    settings.configure(
        DEBUG=False,
        DATABASES={
            'default': {
                'ENGINE': 'django.db.backends.sqlite3',
                'NAME': ':memory:',
            }
        },
        INSTALLED_APPS=[
            'django.contrib.contenttypes',
            'django.contrib.auth',
            'ledger',
        ],
        TIME_ZONE='UTC',
        USE_TZ=True,
    )

django.setup()

from django.core.management import call_command
call_command('migrate', verbosity=0)

from ledger.models import Group, Member, Expense, ExpenseSplit
from ledger.services import split_equally, compute_balances, compute_settlements

def build_dataset_1():
    # 3 members with rounding cases and mixed payers
    group = Group.objects.create(name="Golden Dataset 1")
    m1 = Member.objects.create(name="Aarav", group=group)
    m2 = Member.objects.create(name="Bhavya", group=group)
    m3 = Member.objects.create(name="Chirag", group=group)
    members = [m1, m2, m3]

    expenses_data = [
        ("Dinner", Decimal('100.00'), m1, [m1, m2, m3]),
        ("Cab", Decimal('50.00'), m2, [m1, m2, m3]),
        ("Groceries", Decimal('25.01'), m3, [m1, m2, m3]),
    ]

    recorded_expenses = []
    for desc, amt, payer, split_mems in expenses_data:
        exp = Expense.objects.create(group=group, paid_by=payer, description=desc, amount=amt)
        shares = split_equally(amt, len(split_mems))
        for m, s in zip(split_mems, shares):
            ExpenseSplit.objects.create(expense=exp, member=m, share_amount=s)
        recorded_expenses.append({
            "description": desc,
            "amount": str(amt),
            "paid_by_name": payer.name,
            "split_among_names": [m.name for m in split_mems]
        })

    balances = compute_balances(group)
    settlements = compute_settlements(balances)

    return {
        "name": group.name,
        "members": [m.name for m in members],
        "expenses": recorded_expenses,
        "expected_balances": [
            {"member_name": m.name, "balance": f"{balances[m]:.2f}"}
            for m in members
        ],
        "expected_settlements": [
            {
                "from_member_name": debtor.name,
                "to_member_name": creditor.name,
                "amount": f"{amount:.2f}"
            }
            for debtor, creditor, amount in settlements
        ]
    }

def build_dataset_2():
    # 6 members with subset splits
    group = Group.objects.create(name="Golden Dataset 2")
    names = ["Aarav", "Bhavya", "Chirag", "Divya", "Esha", "Farhan"]
    members = [Member.objects.create(name=n, group=group) for n in names]

    m_map = {m.name: m for m in members}

    expenses_data = [
        ("Hotel", Decimal('120.00'), m_map["Aarav"], [m_map["Aarav"], m_map["Bhavya"], m_map["Chirag"]]),
        ("Museum", Decimal('90.00'), m_map["Bhavya"], [m_map["Bhavya"], m_map["Divya"], m_map["Farhan"]]),
        ("Activity", Decimal('150.00'), m_map["Esha"], [m_map["Chirag"], m_map["Divya"], m_map["Esha"], m_map["Farhan"]]),
        ("Lunch", Decimal('60.00'), m_map["Chirag"], [m_map["Aarav"], m_map["Chirag"]]),
    ]

    recorded_expenses = []
    for desc, amt, payer, split_mems in expenses_data:
        exp = Expense.objects.create(group=group, paid_by=payer, description=desc, amount=amt)
        shares = split_equally(amt, len(split_mems))
        for m, s in zip(split_mems, shares):
            ExpenseSplit.objects.create(expense=exp, member=m, share_amount=s)
        recorded_expenses.append({
            "description": desc,
            "amount": str(amt),
            "paid_by_name": payer.name,
            "split_among_names": [m.name for m in split_mems]
        })

    balances = compute_balances(group)
    settlements = compute_settlements(balances)

    return {
        "name": group.name,
        "members": [m.name for m in members],
        "expenses": recorded_expenses,
        "expected_balances": [
            {"member_name": m.name, "balance": f"{balances[m]:.2f}"}
            for m in members
        ],
        "expected_settlements": [
            {
                "from_member_name": debtor.name,
                "to_member_name": creditor.name,
                "amount": f"{amount:.2f}"
            }
            for debtor, creditor, amount in settlements
        ]
    }

def build_dataset_3():
    # 10 members with many expenses
    group = Group.objects.create(name="Golden Dataset 3")
    names = [f"Member_{i}" for i in range(1, 11)]
    members = [Member.objects.create(name=n, group=group) for n in names]
    m_map = {m.name: m for m in members}

    expenses_data = [
        ("Villa Rent", Decimal('1000.00'), m_map["Member_1"], members),
        ("Groceries Day 1", Decimal('234.50'), m_map["Member_2"], members[:5]),
        ("Groceries Day 2", Decimal('180.25'), m_map["Member_3"], members[3:8]),
        ("Fuel Car 1", Decimal('85.00'), m_map["Member_4"], [m_map["Member_1"], m_map["Member_2"], m_map["Member_3"], m_map["Member_4"]]),
        ("Fuel Car 2", Decimal('95.00'), m_map["Member_5"], [m_map["Member_5"], m_map["Member_6"], m_map["Member_7"], m_map["Member_8"]]),
        ("Drinks", Decimal('320.00'), m_map["Member_6"], members),
        ("Sightseeing Tickets", Decimal('450.00'), m_map["Member_7"], [m_map["Member_2"], m_map["Member_4"], m_map["Member_6"], m_map["Member_8"], m_map["Member_10"]]),
        ("BBQ Night", Decimal('275.75'), m_map["Member_8"], members),
        ("Beach Games", Decimal('50.00'), m_map["Member_9"], [m_map["Member_9"], m_map["Member_10"]]),
        ("Airport Shuttles", Decimal('200.00'), m_map["Member_10"], members),
    ]

    recorded_expenses = []
    for desc, amt, payer, split_mems in expenses_data:
        exp = Expense.objects.create(group=group, paid_by=payer, description=desc, amount=amt)
        shares = split_equally(amt, len(split_mems))
        for m, s in zip(split_mems, shares):
            ExpenseSplit.objects.create(expense=exp, member=m, share_amount=s)
        recorded_expenses.append({
            "description": desc,
            "amount": str(amt),
            "paid_by_name": payer.name,
            "split_among_names": [m.name for m in split_mems]
        })

    balances = compute_balances(group)
    settlements = compute_settlements(balances)

    return {
        "name": group.name,
        "members": [m.name for m in members],
        "expenses": recorded_expenses,
        "expected_balances": [
            {"member_name": m.name, "balance": f"{balances[m]:.2f}"}
            for m in members
        ],
        "expected_settlements": [
            {
                "from_member_name": debtor.name,
                "to_member_name": creditor.name,
                "amount": f"{amount:.2f}"
            }
            for debtor, creditor, amount in settlements
        ]
    }

def main():
    out_dir = os.path.join(repo_root, 'tests', 'SplitSettle.Tests', 'Golden')
    os.makedirs(out_dir, exist_ok=True)

    d1 = build_dataset_1()
    with open(os.path.join(out_dir, 'dataset1.json'), 'w') as f:
        json.dump(d1, f, indent=2)

    d2 = build_dataset_2()
    with open(os.path.join(out_dir, 'dataset2.json'), 'w') as f:
        json.dump(d2, f, indent=2)

    d3 = build_dataset_3()
    with open(os.path.join(out_dir, 'dataset3.json'), 'w') as f:
        json.dump(d3, f, indent=2)

    print("Golden datasets successfully created in:", out_dir)

if __name__ == '__main__':
    main()
