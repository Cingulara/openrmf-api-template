# openrmf-api-upload
This is the OpenRMF Upload API for uploading a CKL file. It has two calls. It also has a load of 
CKL files that are loaded as SYSTEM templates for later use in uploading SCAP scan (XCCDF) files
for making checklists.

POST to / to save a new checklist

PUT to /{id} to update a new checklist content but keep the rest in tact

/swagger/ gives you the API structure.

## Making your local Docker image
docker build --rm -t openrmf-api-template:0.9 .

## creating the user
* ~/mongodb/bin/mongo 'mongodb://root:myp2ssw0rd@localhost'
* use admin
* db.createUser({ user: "openrmftemplate" , pwd: "openrmf1234!", roles: ["readWriteAnyDatabase"]});
* use openstigtemplate
* db.createCollection("Templates");

## connecting to the database collection straight
~/mongodb/bin/mongo 'mongodb://openrmftemplate:openrmf1234!@localhost/openrmftemplate?authSource=admin'
