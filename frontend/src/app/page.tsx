import { redirect } from "next/navigation";

// The standings table is the natural landing page of a tournament.
export default function HomePage() {
  redirect("/standings");
}
