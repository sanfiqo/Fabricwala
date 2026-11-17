# Location master data

## What a fresh database really contains (measured, `MasterDataCountsTests`)

| | Count |
| --- | ---: |
| States and union territories | **36** (28 states, 8 union territories) |
| Cities | **90** (2 to 5 well known cities per state, from `IndiaMasterData.cs`) |
| Delivery zones | **9** (the nine first-digit areas of the postal code, "North" to "Remote") |
| **Pincodes** | **2**: 452001 (Indore) and 141001 (Ludhiana) |

So the "all India" claim was not true for pincodes: only two pincodes are known after the startup seed. A shop or factory with any other pincode cannot sign up (`pincode_unknown`), cannot save an address, and a cart cannot be priced for it. In Development the demo seed (`DevelopmentSeed`) adds a few more (Jaipur, Delhi, Jammu, Srinagar ...), and nothing else does.

## The import command

A real directory is loaded with one command, which does not start the web server and can be run again at any time:

```powershell
dotnet run --project src/TextileConnect.Api -- import-pincodes C:\data\all-india-pincodes.csv
```

- It uses the normal configuration (`ConnectionStrings:TextileConnect` from user-secrets or the environment). The database must already be migrated.
- The file is a comma separated post office directory with a header row. Needed columns (any order, any capitals): `pincode`, `district` (or `districtname`), `statename` (or `state`). Other columns are ignored. This is the layout of the India Post "All India Pincode Directory".
- Each distinct district of a state becomes a **city** (`Gautam Buddha Nagar`, `Central Delhi`). A district that has the same name as an existing city reuses it, so no duplicate Indore or Ludhiana appears. Old and short state names are understood (`ORISSA`, `JAMMU & KASHMIR`, `DADRA & NAGAR HAVELI`, `PONDICHERRY` ...); a row with a state it does not know is skipped and counted in the summary.
- A pincode that is already in the database is **never changed**, so saved addresses and test data stay where they are. If post offices of one pincode name different districts, the most common one wins.
- Rows with a pincode that is not six digits, starts with 0, or has no district are skipped. The summary says how many rows were read and skipped, how many pincodes and cities were added and how many were already known.

Tested on a sample in the real directory's layout (`PincodeImportTests`). **It has not been run on the real file yet: that needs the owner's choice of the data source (below).**

## Data source: owner decision needed

The directory itself is not in the repository. Options, with what is known about each:

1. **India Post "All India Pincode Directory" on data.gov.in** (official; about 155,000 post office rows, about 19,000 pincodes). Published under the Government Open Data License - India, which allows use with attribution. It is downloaded from the website by a person (it needs a free account); the importer takes the downloaded CSV.
2. A community copy on GitHub: easier to download, not official, may be older, and its licence differs from file to file. Not recommended for a business.

The attribution line for option 1 belongs on a legal or about page of the app.

## Rules for addresses

- The city of an address is always the one of its pincode in this table. It is never taken from what the caller sends.
- A pincode that is not six digits gives `pincode_invalid` (400); one that is not in the table gives `pincode_unknown` (400) with the pincode in the message. This holds at sign-up, for delivery and pickup addresses (`/account/addresses`) and for the business address book (`/businesses/me/addresses`).
- Delivery prices come from the pincode's zone (first digit) or from city to city rates; a pincode that is not in the table cannot be priced and the checkout says "We cannot deliver to this address yet."

## Sourcing hubs

`IndiaMasterData.SourcingHubs` is the only list of hub cities (Surat, Delhi, Ludhiana, Panipat, Jaipur, Tiruppur, Ahmedabad, Mumbai). The seed and the importer flag a city as a hub when its name is in that list. There are no region specific lists in the code (the old `KashmirDeliveryCities` constant was unused and has been removed).
