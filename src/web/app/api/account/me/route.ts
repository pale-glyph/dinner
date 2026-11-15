import { auth, currentUser } from "@clerk/nextjs/server";

const householdServiceUrl = process.env.HOUSEHOLD_SERVICE_URL ?? "http://localhost:18082";

export async function GET() {
  const { userId } = await auth();
  if (!userId) return Response.json({ error: "Unauthorized" }, { status: 401 });

  const user = await currentUser();
  const response = await fetch(`${householdServiceUrl}/api/v1/accounts/me`, {
    headers: {
      "X-Internal-Api-Key": process.env.DINNER_INTERNAL_API_KEY ?? "",
      "X-Clerk-User-Id": userId,
      "X-Account-Email": user?.primaryEmailAddress?.emailAddress ?? "",
      "X-Account-Name": user?.fullName ?? user?.username ?? ""
    },
    cache: "no-store"
  });

  const body = await response.text();
  return new Response(body, { status: response.status, headers: { "Content-Type": "application/json" } });
}
