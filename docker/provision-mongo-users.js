// Run with: mongosh "mongodb://platform-admin:<ADMIN_PASSWORD>@forge-mongo-dev.int.aethericforge.ca:27017/admin?directConnection=true" docker/provision-mongo-users.js
// Recreates the institution-scoped Mongo users for aetheric-web after a dev-cluster rebuild wiped them.
// Passwords match what's written into docker/.env.local by this same change.

const users = [
  { user: "forge-library", pwd: "c9c0febef9932731a3cc0052879839f708c349fb0e9d19e239e619b1675182d4", db: "library" },
  { user: "forge-parallel-you", pwd: "2ef2251af990ac5bf61651ef98a129e40cf7eee2cb5fbdca0f6fff79b99d5049", db: "parallel-you" },
  { user: "forge-decisions", pwd: "e8269d4764652470d8f1424f82fb7466d4aa1396189be773239175e329029db5", db: "decisions" },
  { user: "forge-maintenance", pwd: "edfd9d1244e562831e4a792d32198b500fea6e900cc82726d40e658ae1bc0d71", db: "maintenance" },
];

for (const { user, pwd, db: dbName } of users) {
  const targetDb = db.getSiblingDB(dbName);
  const existing = targetDb.getUsers().users.find(u => u.user === user);
  if (existing) {
    print(`Updating password for existing user ${user} on db ${dbName}`);
    targetDb.changeUserPassword(user, pwd);
  } else {
    print(`Creating user ${user} on db ${dbName}`);
    targetDb.createUser({
      user,
      pwd,
      roles: [{ role: "readWrite", db: dbName }],
      mechanisms: ["SCRAM-SHA-256"],
    });
  }
}

print("Done. Verify per-db with db.getSiblingDB('<dbName>').getUsers()");
