export type ServerResponseDto<T> = {
    Success: boolean;
    Message?: string;
    Data?: T;
}