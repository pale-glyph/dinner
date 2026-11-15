import { auth, currentUser } from "@clerk/nextjs/server";

const serviceUrl = process.env.HOUSEHOLD_SERVICE_URL ?? "http://localhost:18082";

async function serviceHeaders() {
  const { userId } = await auth();
  if (!userId) return null;
  const user = await currentUser();
  return {
    "Content-Type": "application/json",
    "X-Internal-Api-Key": process.env.DINNER_INTERNAL_API_KEY ?? "",
    "X-Clerk-User-Id": userId,
    "X-Account-Email": user?.primaryEmailAddress?.emailAddress ?? "",
    "X-Account-Name": user?.fullName ?? user?.username ?? ""
  };
}

async function forward(path: string, init?: RequestInit) {
  const headers = await serviceHeaders();
  if (!headers) return Response.json({ error: "Unauthorized" }, { status: 401 });
  const response = await fetch(`${serviceUrl}/api/v1/households${path}`, { ...init, headers: { ...headers, ...init?.headers }, cache: "no-store" });
  return new Response(await response.text(), { status: response.status, headers: { "Content-Type": "application/json" } });
}

export async function GET() { return forward("/mine"); }
export async function POST(request: Request) { return forward("", { method: "POST", body: await request.text() }); }
