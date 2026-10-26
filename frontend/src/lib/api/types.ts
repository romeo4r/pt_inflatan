// DTOs mirroring the API contracts. Property names match the JSON (camelCase).

export interface PagedResult<T> {
  data: T[];
  pageNumber: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface PagingParams {
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: "asc" | "desc";
}

export interface TeamResponse {
  id: string;
  name: string;
  code: string;
  coach: string | null;
  isActive: boolean;
  playerCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string | null;
}

export interface TeamFilters extends PagingParams {
  search?: string;
  isActive?: boolean;
}

export interface CreateTeamRequest {
  name: string;
  code: string;
  coach?: string | null;
}

export type PlayerPosition = "Goalkeeper" | "Defender" | "Midfielder" | "Forward";

export const PLAYER_POSITIONS: { value: PlayerPosition; label: string }[] = [
  { value: "Goalkeeper", label: "Portero" },
  { value: "Defender", label: "Defensa" },
  { value: "Midfielder", label: "Mediocampista" },
  { value: "Forward", label: "Delantero" },
];

export interface PlayerResponse {
  id: string;
  teamId: string;
  teamName: string;
  teamCode: string;
  firstName: string;
  lastName: string;
  fullName: string;
  jerseyNumber: number;
  position: PlayerPosition;
  birthDate: string | null;
  isActive: boolean;
  rowVersion: string | null;
}

export interface PlayerFilters extends PagingParams {
  search?: string;
  position?: PlayerPosition;
  isActive?: boolean;
}

export interface CreatePlayerRequest {
  firstName: string;
  lastName: string;
  jerseyNumber: number;
  position: PlayerPosition;
  birthDate?: string | null;
}

export type MatchStatus = "Scheduled" | "Finished" | "Cancelled";

export const MATCH_STATUSES: { value: MatchStatus; label: string }[] = [
  { value: "Scheduled", label: "Programado" },
  { value: "Finished", label: "Finalizado" },
  { value: "Cancelled", label: "Cancelado" },
];

export interface MatchResponse {
  id: string;
  matchDay: number;
  scheduledAtUtc: string;
  venue: string | null;
  status: MatchStatus;
  homeTeamId: string;
  homeTeamName: string;
  homeTeamCode: string;
  awayTeamId: string;
  awayTeamName: string;
  awayTeamCode: string;
  homeScore: number | null;
  awayScore: number | null;
  finishedAtUtc: string | null;
  rowVersion: string | null;
}

export interface GoalResponse {
  id: string;
  playerId: string;
  playerFullName: string;
  teamId: string;
  teamCode: string;
  minute: number | null;
}

export interface MatchDetailResponse {
  match: MatchResponse;
  goals: GoalResponse[];
}

export interface MatchFilters extends PagingParams {
  fromUtc?: string;
  toUtc?: string;
  teamId?: string;
  status?: MatchStatus;
  matchDay?: number;
}

export interface ScheduleMatchRequest {
  homeTeamId: string;
  awayTeamId: string;
  matchDay: number;
  scheduledAtUtc: string;
  venue?: string | null;
}

export interface GoalRequest {
  playerId: string;
  minute: number | null;
}

export interface RegisterMatchResultRequest {
  homeScore: number;
  awayScore: number;
  goals: GoalRequest[];
}

export interface StandingRow {
  position: number;
  teamId: string;
  teamName: string;
  teamCode: string;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  points: number;
}

export interface StandingsResponse {
  rows: StandingRow[];
  finishedMatches: number;
}

export interface TopScorer {
  rank: number;
  playerId: string;
  fullName: string;
  jerseyNumber: number;
  teamId: string;
  teamName: string;
  teamCode: string;
  goals: number;
  matchesScoredIn: number;
}
