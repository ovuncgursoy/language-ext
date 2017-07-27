﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LanguageExt;
using LanguageExt.ClassInstances;
using static LanguageExt.Prelude;
using static LanguageExt.TypeClass;
using Xunit;

namespace LanguageExt.Tests
{
    public class IssuesTests
    {
        /// <summary>
        /// https://github.com/louthy/language-ext/issues/207
        /// </summary>
        public Task<Either<Exception, int>> Issue207() =>
            Initialization
                .BindT(createUserMapping)
                .BindT(addUser);

        public Task<Either<Exception, int>> Issue207_2() =>
            from us in Initialization
            from mu in createUserMapping(us).AsTask()
            from id in addUser(mu)
            select id;

        static Task<Either<Exception, ADUser>> Initialization =>
            Right<Exception, ADUser>(ADUser.New("test user")).AsTask();

        static Either<Exception, UserMapping> createUserMapping(ADUser user) =>
            Right<Exception, UserMapping>(UserMapping.New(user.ToString() + " mapped"));

        static Task<Either<Exception, int>> addUser(UserMapping user) =>
            Right<Exception, int>(user.ToString().Length).AsTask();

        static Try<int> addUser2(UserMapping user) => () =>
            user.ToString().Length;

        static Try<UserMapping> createUserMapping2(ADUser user) => () =>
            UserMapping.New(user.ToString() + " mapped");

        [Fact]
        public TryAsync<int> Issue207_5() =>
            from us in TryAsync<ADUser>(() => throw new Exception("fail"))
            from mu in createUserMapping2(us).ToAsync()
            from id in addUser2(mu).ToAsync()
            select id;

        //https://github.com/louthy/language-ext/issues/242
        [Fact]
        public void Issue208()
        {
            var r = from a in Task.FromResult(Left<Error, int>(Error.New("error 1")))
                    from b in Task.FromResult(Right<Error, int>(1))
                    select a + b;

            Assert.True(r.Result == Left<Error, int>(Error.New("error 1")));

            var r2 = from a in Task.FromResult(Option<int>.None)
                     from b in Task.FromResult(Some(1))
                     select a + b;

            Assert.True(r2.Result == None);
        }

        static void EqPar()
        {
            var eq = par<string, string, bool>(equals<EqStringOrdinalIgnoreCase, string>, "abc");
        }
    }

    public class ADUser : NewType<ADUser, string> { public ADUser(string u) : base(u) { } }
    public class UserMapping : NewType<UserMapping, string> { public UserMapping(string u) : base(u) { } }
}

//https://github.com/louthy/language-ext/issues/242
namespace Core.Tests
{
    using static ExternalSystem;

    public class ExternalOptionsAndEithersTests
    {
        [Fact]
        public async Task what_i_desire()
        {
            Task<Either<Error, Pixel>> GetPixelE(PixelId id) =>
                GetPixel(id).MatchAsync(
                    Some: p => Right<Error, Pixel>(p),
                    None: () => Error.New("pixel not found")
                );

            var program =
                from pixel in GetPixelE(PixelId.New("wkrp"))
                from id in GenerateLinkId(pixel.Value)
                from resource in ScrapeUrl("http://google.com")
                select resource;

            (await program).Match(
                Right: r => Assert.True(false, "this should not pass"),
                Left: e => Assert.Equal("pixel not found", e.Value)
            );
        }

        [Fact]
        public async Task what_im_forced_to_do()
        {
            var program =
                from pixel in GetPixel(PixelId.New("wkrp")).AsTry("pixel not found")
                from id in GenerateLinkId(pixel.Value).AsTry()
                from resource in ScrapeUrl("http://google.com").AsTry()
                select resource;

            (await program.Try()).Match(
                Succ: r =>
                {
                    Assert.True(false, "this should not pass");
                    return unit;
                },
                Fail: e =>
                {
                    Assert.Equal("pixel not found", e.Message);
                    return unit;
                }
            );
        }
    }

    static class Ext
    {
        public static Try<T> AsTry<TL, T>(this Either<TL, T> either) where TL : NewType<TL, string> =>
            Try(either.Match(
                    Left: e => throw new Exception(e.Value),
                    Right: identity
                ));

        public static Try<T> AsTry<T>(this Option<T> option, string error) =>
            Try(option.Match(
                    None: () => throw new Exception(error),
                    Some: identity
                ));

        public static TryAsync<T> AsTry<TL, T>(this Task<Either<TL, T>> task) where TL : NewType<TL, string> =>
            task.Map(AsTry).ToAsync();

        public static TryAsync<T> AsTry<T>(this Task<Option<T>> task, string error) =>
            task.Map(o => o.AsTry(error)).ToAsync();
    }

    static class ExternalSystem
    {
        public class Error : NewType<Error, string>
        {
            public Error(string value) : base(value) { }
        }

        public static Task<Option<Pixel>> GetPixel(PixelId id) =>
            Task.FromResult(Option<Pixel>.None);

        public static Task<Either<Error, string>> GenerateLinkId(PixelId pixelId) =>
            Task.FromResult(Right<Error, string>($"{pixelId}-1234"));

        public static Task<Either<Error, WebResource>> ScrapeUrl(string url) =>
            Task.FromResult(Right<Error, WebResource>(new WebResource(200)));

        public class WebResource : NewType<WebResource, int>
        {
            public WebResource(int value) : base(value) { }
        }

        public class PixelId : NewType<PixelId, string>
        {
            public PixelId(string value) : base(value) { }
        }

        public class Pixel : NewType<Pixel, PixelId>
        {
            public Pixel(PixelId value) : base(value) { }
        }
    }

}
