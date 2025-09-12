export type RecentClientsDto = {
    Clients: ClientDto[];
};

export type ClientDto = {
    ClientId: string;
    Nickname?: string;
    TaskId?: string;
    CreatedAt: string;
    LastActiveAt: string;
};