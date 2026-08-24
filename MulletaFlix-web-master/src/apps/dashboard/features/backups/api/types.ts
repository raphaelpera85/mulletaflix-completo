/**
 * Backup API types
 * These types match the backend DTOs
 */

export interface BackupOptionsDto {
  Metadata: boolean;
  Trickplay: boolean;
  Subtitles: boolean;
  Database: boolean;
}

export interface BackupManifestDto {
  ServerVersion: string;
  BackupEngineVersion: string;
  DateCreated: string;
  Path: string;
  Options: BackupOptionsDto;
}

export interface BackupExecutionHistoryDto {
  Path: string;
  StartTimeUtc: string;
  EndTimeUtc: string;
  Status: TaskCompletionStatus;
  Name: string;
  Key: string;
  Id: string;
  ErrorMessage: string;
  LongErrorMessage: string;
  DurationSeconds: number;
}

export enum TaskCompletionStatus {
  Success = 'Success',
  Failed = 'Failed',
  Aborted = 'Aborted',
  Cancelled = 'Cancelled',
}

export interface BackupRestoreRequestDto {
  ArchiveFileName: string;
}

export interface PointInTimeRestoreRequestDto {
  TargetDate: string; // ISO 8601 date string
}