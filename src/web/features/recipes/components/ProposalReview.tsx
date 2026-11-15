import type { Proposal } from "@/types/api";

export function ProposalReview({
  proposal,
  pending,
  status,
  onSelect,
  onApprove,
}: {
  proposal: Proposal | null;
  pending: Proposal[];
  status: string;
  onSelect: (proposal: Proposal) => void;
  onApprove: () => void;
}) {
  return (
    <div>
      <div className="section-heading">
        <h2>Proposal review</h2>
        {proposal && <button onClick={onApprove}>Approve and save</button>}
      </div>
      {status && <p className="status">{status}</p>}
      {proposal ? (
        <p className="muted">
          A {proposal.kind} proposal is awaiting approval.
        </p>
      ) : (
        pending.map((item) => (
          <p key={item.id}>
            <button className="quiet" onClick={() => onSelect(item)}>
              Review agent {item.kind} proposal
              {item.recipe?.title ? `: ${item.recipe.title}` : ""}
            </button>
          </p>
        ))
      )}
    </div>
  );
}
