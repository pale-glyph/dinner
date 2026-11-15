import { auth, currentUser } from "@clerk/nextjs/server";

const serviceUrl = process.env.HOUSEHOLD_SERVICE_URL ?? "http://localhost:18082";

export async function POST(_: Request, { params }: { params: Promise<{ token: string }> }) {
  const { userId } = await auth();
  if (!userId) return Response.json({ error: "Unauthorized" }, { status: 401 });
  const user = await currentUser(); const { token } = await params;
  const response = await fetch(`${serviceUrl}/api/v1/households/invites/${encodeURIComponent(token)}/join`, {
    method: "POST", cache: "no-store", headers: { "X-Internal-Api-Key": process.env.DINNER_INTERNAL_API_KEY ?? "", "X-Clerk-User-Id": userId, "X-Account-Email": user?.primaryEmailAddress?.emailAddress ?? "", "X-Account-Name": user?.fullName ?? user?.username ?? "" }
  });
  return new Response(await response.text(), { status: response.status, headers: { "Content-Type": "application/json" } });
}
