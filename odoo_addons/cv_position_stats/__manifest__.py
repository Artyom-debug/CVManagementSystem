{
    'name': "CV Position Statistics",

    'summary': "Read-only viewer for imported CV position statistics",

    'description': """
Imports position details and aggregated CV attribute statistics from CVManagementSystem.
    """,

    'author': "CVManagementSystem",

    'category': 'Services',
    'version': '19.0.1.1.0',

    'depends': ['base'],
    'external_dependencies': {'python': ['requests']},

    'data': [
        'security/groups.xml',
        'security/ir.model.access.csv',
        'views/views.xml',
    ],
    'application': True,
}
