![.NET Core Build and Test](https://github.com/Cingulara/openrmf-api-template/workflows/.NET%20Core%20Build%20and%20Test/badge.svg)

# openrmf-api-template
This is the OpenRMF Template API for uploading and saving a CKL file as a template. It has two calls. It also has a load of XCCDF files that are loaded as SYSTEM templates for later use in uploading SCAP scan (XCCDF) files
for making checklists.

* POST / to save a new checklist
* PUT /{id} to update a new checklist content but keep the rest in tact
* GET / to get a list of all templates
* GET /{id} to get a specific template
* GET /download/{id} to get the CKL file of the template
* DELETE /{id} to delete a template
* GET /checklistupdate/system/{systemGroupId}/artifact/{artifactId} to see if you need an update
* GET /count/templates to get a count of all user templates
* GET /count/systemtemplates to get a count of all system generated templates (From manual XCCDF files)
* /swagger/ gives you the API structure.

## Making your local Docker image
docker build --rm -t openrmf-api-template:0.13 .

## creating the user
* ~/mongodb/bin/mongo 'mongodb://root:myp2ssw0rd@localhost'
* use admin
* db.createUser({ user: "openrmftemplate" , pwd: "openrmf1234!", roles: ["readWriteAnyDatabase"]});
* use openstigtemplate
* db.createCollection("Templates");

## connecting to the database collection straight
~/mongodb/bin/mongo 'mongodb://openrmftemplate:openrmf1234!@localhost/openrmftemplate?authSource=admin'
