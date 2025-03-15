# AdvancedCustomDataFiltering
 I created this small app to showcase a template I created for highly advanced custom data filtering. The goal of this project was to create a way for users to be able to endlessly filter data in a data table with as many where clauses as they want.

 the "/list" page showcases this workflow. Each column can be dynamically shown/hidden and the user can add as many where clauses as they would like via the column filter button.

 The filtering logic for this page relies heavily on reflection to dynamically create an expression that can be used to filter a queryable object. The main service method simply takes a request model and iterates through the filters the user has selected for each attribute of the entity dataset they are querying.

 The code is not perfect and this project was very experimental, but I hope you find it interesting!
