export type Middleware = {
  onRequest?: (options: RequestInit) => Promise<RequestInit>;
  onResponse?: (response: Response) => Promise<Response>;
};

export abstract class ClientBase {
  private readonly middleware: Middleware[] = [];

  public withMiddleware(middleware: Middleware): this {
    this.middleware.push(middleware);
    return this;
  }

  protected transformOptions = (options: RequestInit): Promise<RequestInit> => {
    return this.middleware.reduce((promise, middleware) => {
      return promise.then(middleware.onRequest);
    }, Promise.resolve(options));
  };

  protected transformResult = (
    _url: string,
    response: Response,
    processor: (response: Response) => any,
  ) => {
    const processedResponse = this.middleware.reduce((promise, middleware) => {
      return promise.then(middleware.onResponse);
    }, Promise.resolve(response));

    return processedResponse.then(processor);
  };
}
